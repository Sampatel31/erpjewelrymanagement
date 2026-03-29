using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;
using ShingarERP.Services;

namespace ShingarERP.Tests.Services
{
    [TestFixture]
    public class SubContractorServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private SubContractorRepository _scRepo = null!;
        private ChallanRepository _challanRepo = null!;
        private SubContractorService _service = null!;

        private static readonly List<(string ItemDescription, int? FinishedGoodId, decimal Qty, string Unit, decimal Rate)> DefaultLines
            = new() { ("Gold Ring", null, 2m, "pcs", 15000m) };

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context     = new ShingarContext(options);
            _uow         = new UnitOfWork(_context);
            _scRepo      = new SubContractorRepository(_context);
            _challanRepo = new ChallanRepository(_context);
            _service     = new SubContractorService(_uow, _scRepo, _challanRepo, NullLogger<SubContractorService>.Instance);
        }

        [TearDown]
        public void TearDown() { _uow.Dispose(); _context.Dispose(); }

        private async Task<SubContractor> CreateTestSCAsync()
            => await _service.OnboardSubContractorAsync("Test SC", "9000000001", null, null, "Casting", "30 days", null);

        // ── OnboardSubContractorAsync ─────────────────────────────────────

        [Test]
        public async Task OnboardSubContractorAsync_ValidInput_CreatesSC()
        {
            var sc = await CreateTestSCAsync();
            Assert.That(sc.Id, Is.GreaterThan(0));
            Assert.That(sc.Name, Is.EqualTo("Test SC"));
            Assert.That(sc.IsActive, Is.True);
        }

        [Test]
        public async Task OnboardSubContractorAsync_EmptyName_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.OnboardSubContractorAsync("", null, null, null, null, null, null));
        }

        [Test]
        public async Task OnboardSubContractorAsync_DefaultPerformanceScore_IsThree()
        {
            var sc = await CreateTestSCAsync();
            Assert.That(sc.PerformanceScore, Is.EqualTo(3));
        }

        // ── CreateChallanAsync ────────────────────────────────────────────

        [Test]
        public async Task CreateChallanAsync_ValidInput_CreatesChallan()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, DateTime.UtcNow.AddDays(14), 1, DefaultLines);
            Assert.That(challan.Id, Is.GreaterThan(0));
            Assert.That(challan.Status, Is.EqualTo("Draft"));
            Assert.That(challan.ChallanNo, Does.StartWith("CH-"));
            Assert.That(challan.TotalAmount, Is.EqualTo(30000));
        }

        [Test]
        public async Task CreateChallanAsync_EmptyLines_ThrowsException()
        {
            var sc = await CreateTestSCAsync();
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateChallanAsync(sc.Id, null, 1, new List<(string, int?, decimal, string, decimal)>()));
        }

        [Test]
        public async Task CreateChallanAsync_MultipleLines_SumsTotalCorrectly()
        {
            var sc = await CreateTestSCAsync();
            var lines = new List<(string, int?, decimal, string, decimal)>
            {
                ("Ring", null, 2m, "pcs", 15000m),
                ("Bangle", null, 1m, "pcs", 20000m)
            };
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, lines);
            Assert.That(challan.TotalAmount, Is.EqualTo(50000));
        }

        // ── AdvanceChallanStatusAsync ─────────────────────────────────────

        [Test]
        public async Task AdvanceChallanStatusAsync_DraftToSent_Succeeds()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            var updated = await _service.AdvanceChallanStatusAsync(challan.Id, "Sent");
            Assert.That(updated.Status, Is.EqualTo("Sent"));
        }

        [Test]
        public async Task AdvanceChallanStatusAsync_InvalidTransition_ThrowsException()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdvanceChallanStatusAsync(challan.Id, "Accepted")); // Must go through Received first
        }

        [Test]
        public async Task AdvanceChallanStatusAsync_DraftToCancelled_Succeeds()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            var cancelled = await _service.AdvanceChallanStatusAsync(challan.Id, "Cancelled");
            Assert.That(cancelled.Status, Is.EqualTo("Cancelled"));
        }

        // ── ReceiveGoodsAsync ─────────────────────────────────────────────

        [Test]
        public async Task ReceiveGoodsAsync_ValidInput_CreatesReceival()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            await _service.AdvanceChallanStatusAsync(challan.Id, "Sent");
            var receival = await _service.ReceiveGoodsAsync(challan.Id, 2, 2, 0, "Accepted", null, 1);
            Assert.That(receival.Id, Is.GreaterThan(0));
            Assert.That(receival.QCStatus, Is.EqualTo("Accepted"));
        }

        [Test]
        public async Task ReceiveGoodsAsync_NotSentStatus_ThrowsException()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            // Still in Draft - cannot receive
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ReceiveGoodsAsync(challan.Id, 2, 2, 0, "Accepted", null, 1));
        }

        [Test]
        public async Task ReceiveGoodsAsync_AdvancesChallanToReceived()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            await _service.AdvanceChallanStatusAsync(challan.Id, "Sent");
            await _service.ReceiveGoodsAsync(challan.Id, 2, 2, 0, "Accepted", null, 1);
            var updated = await _challanRepo.GetByIdAsync(challan.Id);
            Assert.That(updated!.Status, Is.EqualTo("Received"));
        }

        [Test]
        public async Task ReceiveGoodsAsync_InvalidQCStatus_ThrowsException()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            await _service.AdvanceChallanStatusAsync(challan.Id, "Sent");
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ReceiveGoodsAsync(challan.Id, 2, 2, 0, "Unknown", null, 1));
        }

        // ── ProcessPaymentAsync ───────────────────────────────────────────

        [Test]
        public async Task ProcessPaymentAsync_AcceptedChallan_RecordsPayment()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            await _service.AdvanceChallanStatusAsync(challan.Id, "Sent");
            await _service.ReceiveGoodsAsync(challan.Id, 2, 2, 0, "Accepted", null, 1);
            await _service.AdvanceChallanStatusAsync(challan.Id, "Accepted");
            var payment = await _service.ProcessPaymentAsync(challan.Id, 30000, "Bank", "REF001", null, 1);
            Assert.That(payment.Id, Is.GreaterThan(0));
            Assert.That(payment.Amount, Is.EqualTo(30000));
        }

        [Test]
        public async Task ProcessPaymentAsync_FullPayment_MarksChallanAsPaid()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            await _service.AdvanceChallanStatusAsync(challan.Id, "Sent");
            await _service.ReceiveGoodsAsync(challan.Id, 2, 2, 0, "Accepted", null, 1);
            await _service.AdvanceChallanStatusAsync(challan.Id, "Accepted");
            await _service.ProcessPaymentAsync(challan.Id, 30000, "Cash", null, null, 1);
            var updated = await _challanRepo.GetByIdAsync(challan.Id);
            Assert.That(updated!.Status, Is.EqualTo("Paid"));
        }

        [Test]
        public async Task ProcessPaymentAsync_DraftChallan_ThrowsException()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ProcessPaymentAsync(challan.Id, 30000, "Cash", null, null, 1));
        }

        [Test]
        public async Task ProcessPaymentAsync_OverpaymentAttempt_ThrowsException()
        {
            var sc = await CreateTestSCAsync();
            var challan = await _service.CreateChallanAsync(sc.Id, null, 1, DefaultLines);
            await _service.AdvanceChallanStatusAsync(challan.Id, "Sent");
            await _service.ReceiveGoodsAsync(challan.Id, 2, 2, 0, "Accepted", null, 1);
            await _service.AdvanceChallanStatusAsync(challan.Id, "Accepted");
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ProcessPaymentAsync(challan.Id, 50000, "Cash", null, null, 1)); // Exceeds 30000
        }

        // ── GetOverdueChallansAsync ────────────────────────────────────────

        [Test]
        public async Task GetOverdueChallansAsync_ReturnsOverdueOnly()
        {
            var sc = await CreateTestSCAsync();
            var overdueChallan = await _service.CreateChallanAsync(sc.Id, DateTime.UtcNow.AddDays(-2), 1, DefaultLines);
            await _service.AdvanceChallanStatusAsync(overdueChallan.Id, "Sent");

            var futureChallan = await _service.CreateChallanAsync(sc.Id, DateTime.UtcNow.AddDays(10), 1, DefaultLines);
            await _service.AdvanceChallanStatusAsync(futureChallan.Id, "Sent");

            var overdue = await _service.GetOverdueChallansAsync();
            Assert.That(overdue.Select(c => c.Id), Does.Contain(overdueChallan.Id));
            Assert.That(overdue.Select(c => c.Id), Does.Not.Contain(futureChallan.Id));
        }
    }
}
