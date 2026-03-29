using System;
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
    public class ManufacturingServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private JobCardRepository _jobCardRepo = null!;
        private ManufacturingService _service = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context     = new ShingarContext(options);
            _uow         = new UnitOfWork(_context);
            _jobCardRepo = new JobCardRepository(_context);
            _service     = new ManufacturingService(_uow, _jobCardRepo, NullLogger<ManufacturingService>.Instance);
        }

        [TearDown]
        public void TearDown() { _uow.Dispose(); _context.Dispose(); }

        // ── CreateJobCardAsync ──────────────────────────────────────────────

        [Test]
        public async Task CreateJobCardAsync_ValidInput_CreatesJobCard()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, "Test instructions", 1);
            Assert.That(jc.Id, Is.GreaterThan(0));
            Assert.That(jc.Status, Is.EqualTo("Draft"));
            Assert.That(jc.JobCardNo, Does.StartWith("JC-"));
        }

        [Test]
        public async Task CreateJobCardAsync_CreatesDefaultStages()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            var saved = await _jobCardRepo.GetWithDetailsAsync(jc.Id);
            Assert.That(saved!.Stages.Count, Is.EqualTo(5));
            Assert.That(saved.Stages.Select(s => s.StageName), Does.Contain("Casting"));
        }

        [Test]
        public async Task CreateJobCardAsync_WithDesignAndKarigar_SavesCorrectly()
        {
            var jc = await _service.CreateJobCardAsync(1, 2, 3, DateTime.UtcNow.AddDays(5), 10, 8000, null, 1);
            Assert.That(jc.SalesOrderId, Is.EqualTo(1));
            Assert.That(jc.DesignId, Is.EqualTo(2));
            Assert.That(jc.KarigarId, Is.EqualTo(3));
        }

        // ── AdvanceStageAsync ────────────────────────────────────────────────

        [Test]
        public async Task AdvanceStageAsync_DraftToMaterialAllotment_Succeeds()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            var updated = await _service.AdvanceStageAsync(jc.Id, "MaterialAllotment", 1);
            Assert.That(updated.Status, Is.EqualTo("MaterialAllotment"));
        }

        [Test]
        public async Task AdvanceStageAsync_InvalidTransition_ThrowsException()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdvanceStageAsync(jc.Id, "Finishing", 1));
        }

        [Test]
        public async Task AdvanceStageAsync_ThroughFullWorkflow_SetsCompletedDate()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            await _service.AdvanceStageAsync(jc.Id, "MaterialAllotment", 1);
            await _service.AdvanceStageAsync(jc.Id, "Casting", 1);
            await _service.AdvanceStageAsync(jc.Id, "Setting", 1);
            await _service.AdvanceStageAsync(jc.Id, "Finishing", 1);
            await _service.AdvanceStageAsync(jc.Id, "QCGate", 1);
            var completed = await _service.AdvanceStageAsync(jc.Id, "Ready", 1);
            Assert.That(completed.Status, Is.EqualTo("Ready"));
            Assert.That(completed.CompletedDate, Is.Not.Null);
        }

        [Test]
        public async Task AdvanceStageAsync_NonExistentJobCard_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdvanceStageAsync(99999, "MaterialAllotment", 1));
        }

        [Test]
        public async Task AdvanceStageAsync_RecordsHistory()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            await _service.AdvanceStageAsync(jc.Id, "MaterialAllotment", 1, "Ready for allotment");
            var withHistory = await _jobCardRepo.GetWithDetailsAsync(jc.Id);
            Assert.That(withHistory!.History.Count, Is.EqualTo(1));
            Assert.That(withHistory.History.First().ToStatus, Is.EqualTo("MaterialAllotment"));
        }

        [Test]
        public async Task AdvanceStageAsync_QCGateToRework_Succeeds()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            await _service.AdvanceStageAsync(jc.Id, "MaterialAllotment", 1);
            await _service.AdvanceStageAsync(jc.Id, "Casting", 1);
            await _service.AdvanceStageAsync(jc.Id, "Setting", 1);
            await _service.AdvanceStageAsync(jc.Id, "Finishing", 1);
            await _service.AdvanceStageAsync(jc.Id, "QCGate", 1);
            var rework = await _service.AdvanceStageAsync(jc.Id, "Rework", 1);
            Assert.That(rework.Status, Is.EqualTo("Rework"));
        }

        // ── RecordLaborAsync ─────────────────────────────────────────────────

        [Test]
        public async Task RecordLaborAsync_ValidInput_CreatesLabor()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            var labor = await _service.RecordLaborAsync(jc.Id, 1, 4, 150, DateTime.UtcNow);
            Assert.That(labor.LaborCost, Is.EqualTo(600));
        }

        [Test]
        public async Task RecordLaborAsync_UpdatesJobCardActualCost()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            await _service.RecordLaborAsync(jc.Id, 1, 4, 150, DateTime.UtcNow);
            var updated = await _jobCardRepo.GetByIdAsync(jc.Id);
            Assert.That(updated!.ActualCost, Is.EqualTo(600));
        }

        [Test]
        public async Task RecordLaborAsync_ZeroHours_ThrowsException()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordLaborAsync(jc.Id, 1, 0, 150, DateTime.UtcNow));
        }

        // ── RecordMaterialAsync ───────────────────────────────────────────────

        [Test]
        public async Task RecordMaterialAsync_ValidInput_CreatesMaterial()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            var mat = await _service.RecordMaterialAsync(jc.Id, "Gold", 10.5m, 10m, 5000m);
            Assert.That(mat.TotalCost, Is.EqualTo(52500));
        }

        [Test]
        public async Task RecordMaterialAsync_NegativeQty_ThrowsException()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RecordMaterialAsync(jc.Id, "Gold", -1, 10m, 5000m));
        }

        // ── CalculateLaborCostAsync ───────────────────────────────────────────

        [Test]
        public async Task CalculateLaborCostAsync_MultipleLaborEntries_ReturnsSum()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            await _service.RecordLaborAsync(jc.Id, 1, 4, 150, DateTime.UtcNow);
            await _service.RecordLaborAsync(jc.Id, 2, 2, 200, DateTime.UtcNow);
            var total = await _service.CalculateLaborCostAsync(jc.Id);
            Assert.That(total, Is.EqualTo(1000)); // 600 + 400
        }

        // ── GetOverdueJobCardsAsync ─────────────────────────────────────────

        [Test]
        public async Task GetOverdueJobCardsAsync_ReturnsOverdueOnly()
        {
            // Overdue job card
            var jcOverdue = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(-1), 8, 5000, null, 1);

            // Non-overdue job card
            var jcFuture = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);

            var overdue = await _service.GetOverdueJobCardsAsync();
            Assert.That(overdue.Select(j => j.Id), Does.Contain(jcOverdue.Id));
            Assert.That(overdue.Select(j => j.Id), Does.Not.Contain(jcFuture.Id));
        }

        [Test]
        public async Task AdvanceStageAsync_DraftToCancelled_Succeeds()
        {
            var jc = await _service.CreateJobCardAsync(null, null, null, DateTime.UtcNow.AddDays(7), 8, 5000, null, 1);
            var cancelled = await _service.AdvanceStageAsync(jc.Id, "Cancelled", 1, "Customer cancelled");
            Assert.That(cancelled.Status, Is.EqualTo("Cancelled"));
        }
    }
}
