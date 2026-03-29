using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;
using ShingarERP.Services;

namespace ShingarERP.Tests.Services
{
    [TestFixture]
    public class MultiLocationInventoryServiceTests
    {
        private ShingarContext                  _context  = null!;
        private UnitOfWork                      _uow      = null!;
        private LocationInventoryRepository     _locInvRepo = null!;
        private StockTransferRepository         _transferRepo = null!;
        private MultiLocationInventoryService   _service  = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase($"MLITest_{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context      = new ShingarContext(options);
            _uow          = new UnitOfWork(_context);
            _locInvRepo   = new LocationInventoryRepository(_context);
            _transferRepo = new StockTransferRepository(_context);
            _service      = new MultiLocationInventoryService(
                _uow, _locInvRepo, _transferRepo,
                NullLogger<MultiLocationInventoryService>.Instance);

            SeedTestData();
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        // ── Location CRUD ─────────────────────────────────────────────

        [Test]
        public async Task CreateLocationAsync_ValidData_ShouldCreateLocation()
        {
            var loc = await _service.CreateLocationAsync("LOC-NEW", "New Branch", "Showroom");
            Assert.That(loc.LocationCode, Is.EqualTo("LOC-NEW"));
            Assert.That(loc.IsActive,     Is.True);
        }

        [Test]
        public async Task CreateLocationAsync_DuplicateCode_ShouldThrow()
        {
            await _service.CreateLocationAsync("LOC-DUP", "Dup Branch", "Showroom");
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateLocationAsync("LOC-DUP", "Other Branch", "Warehouse"));
            Assert.That(ex!.Message, Does.Contain("already exists"));
        }

        [Test]
        public async Task GetActiveLocationsAsync_ShouldReturnOnlyActiveLocations()
        {
            await _service.CreateLocationAsync("ACT-1", "Active 1", "Showroom");
            await _service.CreateLocationAsync("ACT-2", "Active 2", "Warehouse");
            var locations = await _service.GetActiveLocationsAsync();
            Assert.That(locations, Is.Not.Empty);
        }

        // ── Stock Level ───────────────────────────────────────────────

        [Test]
        public async Task GetStockLevelAsync_NoStock_ShouldReturnZero()
        {
            var qty = await _service.GetStockLevelAsync(1, 1);
            Assert.That(qty, Is.EqualTo(0));
        }

        [Test]
        public async Task AdjustStockAsync_Positive_ShouldIncreaseQuantity()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Initial load");
            var qty = await _service.GetStockLevelAsync(1, 1);
            Assert.That(qty, Is.EqualTo(10));
        }

        [Test]
        public async Task AdjustStockAsync_Negative_ShouldDecreaseQuantity()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Load");
            await _service.AdjustStockAsync(1, 1, -3, "Sale");
            var qty = await _service.GetStockLevelAsync(1, 1);
            Assert.That(qty, Is.EqualTo(7));
        }

        [Test]
        public async Task AdjustStockAsync_BelowZero_ShouldThrow()
        {
            await _service.AdjustStockAsync(1, 1, 5, "Load");
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdjustStockAsync(1, 1, -10, "Oversell"));
            Assert.That(ex!.Message, Does.Contain("Insufficient stock"));
        }

        [Test]
        public async Task GetTotalStockAsync_AcrossLocations_ShouldSumAll()
        {
            await _service.AdjustStockAsync(1, 1, 5, "Load loc 1");
            await _service.AdjustStockAsync(2, 1, 8, "Load loc 2");
            var total = await _service.GetTotalStockAsync(1);
            Assert.That(total, Is.EqualTo(13));
        }

        [Test]
        public async Task GetStockDistributionAsync_ShouldReturnPerLocationBreakdown()
        {
            await _service.AdjustStockAsync(1, 1, 4, "Load");
            await _service.AdjustStockAsync(2, 1, 6, "Load");
            var dist = await _service.GetStockDistributionAsync(1);
            Assert.That(dist.Count, Is.EqualTo(2));
        }

        // ── Transfer ──────────────────────────────────────────────────

        [Test]
        public async Task InitiateTransferAsync_ValidData_ShouldCreatePendingTransfer()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Load");
            var transfer = await _service.InitiateTransferAsync(1, 2, 1, 3, "Test transfer");
            Assert.That(transfer.Status,   Is.EqualTo("Pending"));
            Assert.That(transfer.Quantity, Is.EqualTo(3));
        }

        [Test]
        public async Task InitiateTransferAsync_InsufficientStock_ShouldThrow()
        {
            await _service.AdjustStockAsync(1, 1, 2, "Load");
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.InitiateTransferAsync(1, 2, 1, 10));
            Assert.That(ex!.Message, Does.Contain("Insufficient stock"));
        }

        [Test]
        public async Task InitiateTransferAsync_SameSourceDest_ShouldThrow()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Load");
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.InitiateTransferAsync(1, 1, 1, 5));
            Assert.That(ex!.Message, Does.Contain("different"));
        }

        [Test]
        public async Task InitiateTransferAsync_ZeroQuantity_ShouldThrow()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Load");
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _service.InitiateTransferAsync(1, 2, 1, 0));
        }

        [Test]
        public async Task CompleteTransferAsync_ValidTransfer_ShouldMoveStock()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Load");
            var transfer = await _service.InitiateTransferAsync(1, 2, 1, 4);
            await _service.CompleteTransferAsync(transfer.TransferId, "Manager");

            var srcQty = await _service.GetStockLevelAsync(1, 1);
            var dstQty = await _service.GetStockLevelAsync(2, 1);
            Assert.That(srcQty, Is.EqualTo(6));
            Assert.That(dstQty, Is.EqualTo(4));
        }

        [Test]
        public async Task CompleteTransferAsync_AlreadyCompleted_ShouldThrow()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Load");
            var transfer = await _service.InitiateTransferAsync(1, 2, 1, 4);
            await _service.CompleteTransferAsync(transfer.TransferId);
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CompleteTransferAsync(transfer.TransferId));
            Assert.That(ex!.Message, Does.Contain("Completed"));
        }

        [Test]
        public async Task CancelTransferAsync_PendingTransfer_ShouldCancel()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Load");
            var transfer = await _service.InitiateTransferAsync(1, 2, 1, 4);
            await _service.CancelTransferAsync(transfer.TransferId, "No longer needed");

            var t = await _uow.Repository<StockTransfer>().GetByIdAsync(transfer.TransferId);
            Assert.That(t!.Status, Is.EqualTo("Cancelled"));
        }

        [Test]
        public async Task CancelTransferAsync_CompletedTransfer_ShouldThrow()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Load");
            var transfer = await _service.InitiateTransferAsync(1, 2, 1, 4);
            await _service.CompleteTransferAsync(transfer.TransferId);
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CancelTransferAsync(transfer.TransferId, "Too late"));
            Assert.That(ex!.Message, Does.Contain("Completed"));
        }

        // ── Balancing ─────────────────────────────────────────────────

        [Test]
        public async Task GetBalancingSuggestionsAsync_Imbalanced_ShouldSuggestTransfer()
        {
            await _service.AdjustStockAsync(1, 1, 20, "Load loc 1");
            await _service.AdjustStockAsync(2, 1,  0, "Load loc 2");
            var suggestions = await _service.GetBalancingSuggestionsAsync(1);
            Assert.That(suggestions, Is.Not.Empty);
        }

        [Test]
        public async Task GetBalancingSuggestionsAsync_Balanced_ShouldReturnEmpty()
        {
            await _service.AdjustStockAsync(1, 1, 10, "Load");
            await _service.AdjustStockAsync(2, 1, 10, "Load");
            var suggestions = await _service.GetBalancingSuggestionsAsync(1);
            Assert.That(suggestions, Is.Empty);
        }

        [Test]
        public async Task GetLocationSummaryAsync_ShouldReturnSummaryForAllLocations()
        {
            await _service.AdjustStockAsync(1, 1, 5, "Load");
            var summary = await _service.GetLocationSummaryAsync();
            Assert.That(summary, Is.Not.Null);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void SeedTestData()
        {
            _context.Metals.Add(
                new Metal { MetalId = 1, MetalType = "Gold", PurityCode = "22K", Fineness = 916, WeightUnit = "g" });
            _context.ItemCategories.Add(
                new ItemCategory { CategoryId = 1, CategoryName = "Ring", CategoryCode = "RNG" });
            _context.FinishedGoods.Add(
                new FinishedGood
                {
                    ItemId = 1, SKU = "RNG-001", ItemName = "Test Ring",
                    CategoryId = 1, MetalId = 1, GrossWeight = 5, NetWeight = 4.5m,
                    SalePrice = 30000, StockLocation = "Showroom"
                });
            _context.InventoryLocations.AddRange(
                new InventoryLocation { LocationId = 1, LocationCode = "LOC-1", LocationName = "Main Showroom", LocationType = "Showroom" },
                new InventoryLocation { LocationId = 2, LocationCode = "LOC-2", LocationName = "Warehouse",     LocationType = "Warehouse" }
            );
            _context.SaveChanges();
        }
    }
}
