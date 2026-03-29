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
    public class DesignManagementServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private DesignRepository _designRepo = null!;
        private DesignManagementService _service = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context   = new ShingarContext(options);
            _uow       = new UnitOfWork(_context);
            _designRepo = new DesignRepository(_context);
            _service   = new DesignManagementService(_uow, _designRepo, NullLogger<DesignManagementService>.Instance);
        }

        [TearDown]
        public void TearDown() { _uow.Dispose(); _context.Dispose(); }

        private async Task<Design> CreateTestDesignAsync(string code = "D001")
            => await _service.CreateDesignAsync(code, "Test Design", null, "Gold", "Medium", 8, 15, 50000, null, null);

        // ── CreateDesignAsync ────────────────────────────────────────────

        [Test]
        public async Task CreateDesignAsync_ValidInput_CreatesDesign()
        {
            var d = await CreateTestDesignAsync();
            Assert.That(d.Id, Is.GreaterThan(0));
            Assert.That(d.Status, Is.EqualTo("Draft"));
            Assert.That(d.DesignCode, Is.EqualTo("D001"));
        }

        [Test]
        public async Task CreateDesignAsync_WithBOM_CreatesBOMItems()
        {
            var bom = new List<(string, string?, decimal, string, decimal)>
            {
                ("Gold", "22K metal", 15m, "g", 4800m),
                ("Diamond", "0.5ct", 1m, "ct", 15000m)
            };
            var d = await _service.CreateDesignAsync("D002", "Diamond Ring", null, "Gold", "Complex", 12, 15, 80000, null, null, bom);
            var saved = await _designRepo.GetWithDetailsAsync(d.Id);
            Assert.That(saved!.BOMs.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task CreateDesignAsync_EmptyCode_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateDesignAsync("", "Test", null, null, "Simple", 4, 10, 20000, null, null));
        }

        [Test]
        public async Task CreateDesignAsync_RecordsInitialHistory()
        {
            var d = await CreateTestDesignAsync();
            var saved = await _designRepo.GetWithDetailsAsync(d.Id);
            Assert.That(saved!.History.Count, Is.EqualTo(1));
            Assert.That(saved.History.First().Version, Is.EqualTo(1));
        }

        // ── AdvanceStatusAsync ────────────────────────────────────────────

        [Test]
        public async Task AdvanceStatusAsync_DraftToReview_Succeeds()
        {
            var d = await CreateTestDesignAsync();
            var updated = await _service.AdvanceStatusAsync(d.Id, "Review", 1);
            Assert.That(updated.Status, Is.EqualTo("Review"));
        }

        [Test]
        public async Task AdvanceStatusAsync_InvalidTransition_ThrowsException()
        {
            var d = await CreateTestDesignAsync();
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdvanceStatusAsync(d.Id, "Active", 1));
        }

        [Test]
        public async Task AdvanceStatusAsync_FullApprovalWorkflow_Succeeds()
        {
            var d = await CreateTestDesignAsync();
            await _service.AdvanceStatusAsync(d.Id, "Review", 1);
            await _service.AdvanceStatusAsync(d.Id, "Approved", 1);
            var active = await _service.AdvanceStatusAsync(d.Id, "Active", 1);
            Assert.That(active.Status, Is.EqualTo("Active"));
        }

        [Test]
        public async Task AdvanceStatusAsync_RecordsHistory()
        {
            var d = await CreateTestDesignAsync();
            await _service.AdvanceStatusAsync(d.Id, "Review", 1, "Sent for review");
            var saved = await _designRepo.GetWithDetailsAsync(d.Id);
            Assert.That(saved!.History.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task AdvanceStatusAsync_NonExistentDesign_ThrowsException()
        {
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdvanceStatusAsync(99999, "Review", 1));
        }

        // ── EstimateCostAsync ─────────────────────────────────────────────

        [Test]
        public async Task EstimateCostAsync_WithBOM_CalculatesCorrectly()
        {
            var bom = new List<(string, string?, decimal, string, decimal)>
            {
                ("Gold", null, 10m, "g", 5000m) // 50000 material
            };
            var d = await _service.CreateDesignAsync("D003", "Ring", null, "Gold", "Simple", 4, 10, 0, null, null, bom);
            // material = 10*5000=50000, metal = 10*4800=48000, labor = 4*200=800
            var cost = await _service.EstimateCostAsync(d.Id, 4800, 200, 20);
            Assert.That(cost, Is.GreaterThan(0));
        }

        [Test]
        public async Task EstimateCostAsync_AppliesMarkup()
        {
            var d = await CreateTestDesignAsync();
            var costZeroMarkup  = await _service.EstimateCostAsync(d.Id, 4800, 200, 0);
            var cost20Markup    = await _service.EstimateCostAsync(d.Id, 4800, 200, 20);
            Assert.That(cost20Markup, Is.GreaterThan(costZeroMarkup));
        }

        // ── IncrementPopularityAsync ─────────────────────────────────────

        [Test]
        public async Task IncrementPopularityAsync_IncrementsScore()
        {
            var d = await CreateTestDesignAsync();
            await _service.IncrementPopularityAsync(d.Id);
            await _service.IncrementPopularityAsync(d.Id);
            var updated = await _designRepo.GetByIdAsync(d.Id);
            Assert.That(updated!.PopularityScore, Is.EqualTo(2));
        }

        // ── GetTrendingDesignsAsync ─────────────────────────────────────

        [Test]
        public async Task GetTrendingDesignsAsync_ReturnsByPopularity()
        {
            var d1 = await CreateTestDesignAsync("T001");
            var d2 = await _service.CreateDesignAsync("T002", "Design 2", null, "Gold", "Simple", 4, 10, 0, null, null);
            // Approve d1 and make active
            await _service.AdvanceStatusAsync(d1.Id, "Review", 1);
            await _service.AdvanceStatusAsync(d1.Id, "Approved", 1);
            await _service.AdvanceStatusAsync(d1.Id, "Active", 1);
            await _service.IncrementPopularityAsync(d1.Id);
            await _service.IncrementPopularityAsync(d1.Id);

            await _service.AdvanceStatusAsync(d2.Id, "Review", 1);
            await _service.AdvanceStatusAsync(d2.Id, "Approved", 1);
            await _service.AdvanceStatusAsync(d2.Id, "Active", 1);
            await _service.IncrementPopularityAsync(d2.Id);

            var trending = (await _service.GetTrendingDesignsAsync(5)).ToList();
            Assert.That(trending.First().Id, Is.EqualTo(d1.Id));
        }

        // ── CreateCollectionAsync ─────────────────────────────────────────

        [Test]
        public async Task CreateCollectionAsync_ValidInput_CreatesCollection()
        {
            var c = await _service.CreateCollectionAsync("Diwali 2024", "Autumn", 2024, "Festival collection");
            Assert.That(c.Id, Is.GreaterThan(0));
            Assert.That(c.CollectionName, Is.EqualTo("Diwali 2024"));
            Assert.That(c.IsActive, Is.True);
        }
    }
}
