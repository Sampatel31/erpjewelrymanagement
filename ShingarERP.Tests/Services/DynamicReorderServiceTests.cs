using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;
using ShingarERP.Services;

namespace ShingarERP.Tests.Services
{
    [TestFixture]
    public class DynamicReorderServiceTests
    {
        private ShingarContext        _context      = null!;
        private UnitOfWork            _uow          = null!;
        private ReorderPointRepository _reorderRepo = null!;
        private SalesHistoryRepository _salesRepo   = null!;
        private DynamicReorderService  _service     = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase($"ReorderTest_{Guid.NewGuid()}")
                .Options;

            _context    = new ShingarContext(options);
            _uow        = new UnitOfWork(_context);
            _reorderRepo = new ReorderPointRepository(_context);
            _salesRepo   = new SalesHistoryRepository(_context);
            _service     = new DynamicReorderService(
                _uow, _reorderRepo, _salesRepo,
                NullLogger<DynamicReorderService>.Instance);

            SeedTestData();
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        // ── ABC Analysis ─────────────────────────────────────────────

        [Test]
        public void ClassifyAbc_HighValue_ShouldReturnA()
        {
            var result = _service.ClassifyAbc(10000m, 50000m);  // 20% share
            Assert.That(result, Is.EqualTo("A"));
        }

        [Test]
        public void ClassifyAbc_MediumValue_ShouldReturnB()
        {
            var result = _service.ClassifyAbc(2000m, 50000m);   // 4% share
            Assert.That(result, Is.EqualTo("B"));
        }

        [Test]
        public void ClassifyAbc_LowValue_ShouldReturnC()
        {
            var result = _service.ClassifyAbc(100m, 50000m);    // 0.2% share
            Assert.That(result, Is.EqualTo("C"));
        }

        [Test]
        public void ClassifyAbc_ZeroTotal_ShouldReturnC()
        {
            var result = _service.ClassifyAbc(1000m, 0m);
            Assert.That(result, Is.EqualTo("C"));
        }

        // ── EOQ ───────────────────────────────────────────────────────

        [Test]
        public void CalculateEoq_ValidInputs_ShouldReturnPositive()
        {
            var eoq = _service.CalculateEoq(120, 500m, 200m);
            Assert.That(eoq, Is.GreaterThan(0));
        }

        [Test]
        public void CalculateEoq_ClassicExample_ShouldMatchExpected()
        {
            // D=1000, S=100, H=25 → EOQ = sqrt(2*1000*100/25) = sqrt(8000) ≈ 89
            var eoq = _service.CalculateEoq(1000, 100m, 25m);
            Assert.That(eoq, Is.EqualTo(89).Within(1));
        }

        [Test]
        public void CalculateEoq_ZeroDemand_ShouldThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.CalculateEoq(0, 500m, 200m));
        }

        [Test]
        public void CalculateEoq_ZeroOrderCost_ShouldThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.CalculateEoq(100, 0m, 200m));
        }

        [Test]
        public void CalculateEoq_ZeroHoldingCost_ShouldThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.CalculateEoq(100, 500m, 0m));
        }

        // ── Safety Stock ──────────────────────────────────────────────

        [Test]
        public void CalculateSafetyStock_NormalInputs_ShouldReturnNonNegative()
        {
            var ss = _service.CalculateSafetyStock(5.0, 2.0, 7);
            Assert.That(ss, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void CalculateSafetyStock_ZeroVariability_ShouldReturnZero()
        {
            var ss = _service.CalculateSafetyStock(5.0, 0.0, 7);
            Assert.That(ss, Is.EqualTo(0));
        }

        [Test]
        public void CalculateSafetyStock_ZeroLeadTime_ShouldReturnZero()
        {
            var ss = _service.CalculateSafetyStock(5.0, 2.0, 0);
            Assert.That(ss, Is.EqualTo(0));
        }

        [Test]
        public void CalculateSafetyStock_NegativeLeadTime_ShouldThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.CalculateSafetyStock(5.0, 2.0, -1));
        }

        // ── Reorder Level ─────────────────────────────────────────────

        [Test]
        public void CalculateReorderLevel_ShouldIncludeSafetyStock()
        {
            var rop = _service.CalculateReorderLevel(2.0, 5, 10);
            // demand during lead time = ceil(2*5) = 10, + safety = 20
            Assert.That(rop, Is.EqualTo(20));
        }

        [Test]
        public void CalculateReorderLevel_ZeroDemand_ShouldReturnSafetyStockOnly()
        {
            var rop = _service.CalculateReorderLevel(0.0, 5, 10);
            Assert.That(rop, Is.EqualTo(10));
        }

        // ── Full Recalculation ────────────────────────────────────────

        [Test]
        public async Task RecalculateAsync_WithSalesHistory_ShouldPersistReorderPoint()
        {
            SeedSalesHistory();
            var rp = await _service.RecalculateAsync(1, null, leadTimeDays: 7);
            Assert.That(rp.LeadTimeDays,  Is.EqualTo(7));
            Assert.That(rp.OrderQuantity, Is.GreaterThan(0));
            Assert.That(rp.AbcCategory,   Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task RecalculateAsync_NoSalesHistory_ShouldStillPersist()
        {
            var rp = await _service.RecalculateAsync(1, null, leadTimeDays: 14);
            Assert.That(rp.LeadTimeDays, Is.EqualTo(14));
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
            _context.InventoryLocations.Add(
                new InventoryLocation { LocationId = 1, LocationCode = "LOC-1", LocationName = "Main Showroom", LocationType = "Showroom" });
            _context.SaveChanges();
        }

        private void SeedSalesHistory()
        {
            var now = DateTime.UtcNow;
            for (int i = 3; i >= 1; i--)
            {
                _context.SalesHistories.Add(new SalesHistory
                {
                    InvoiceNo   = $"INV-RO-{i:D3}",
                    ItemId      = 1,
                    LocationId  = 1,
                    Quantity    = 8,
                    UnitPrice   = 30000m,
                    TotalAmount = 240000m,
                    SaleDate    = now.AddMonths(-i)
                });
            }
            _context.SaveChanges();
        }
    }
}
