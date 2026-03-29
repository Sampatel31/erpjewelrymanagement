using System;
using System.Collections.Generic;
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
    public class InventoryForecastingServiceTests
    {
        private ShingarContext              _context      = null!;
        private ForecastDataRepository      _forecastRepo = null!;
        private SalesHistoryRepository      _salesRepo    = null!;
        private InventoryForecastingService _service      = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase($"ForecastTest_{Guid.NewGuid()}")
                .Options;

            _context      = new ShingarContext(options);
            _forecastRepo = new ForecastDataRepository(_context);
            _salesRepo    = new SalesHistoryRepository(_context);
            _service      = new InventoryForecastingService(
                _forecastRepo, _salesRepo,
                NullLogger<InventoryForecastingService>.Instance);

            SeedTestData();
        }

        [TearDown]
        public void TearDown() => _context.Dispose();

        // ── Exponential Smoothing ─────────────────────────────────────

        [Test]
        public void ExponentialSmoothing_ConstantDemand_ShouldReturnSameValue()
        {
            var demand = new List<int> { 10, 10, 10, 10, 10 };
            var result = _service.ExponentialSmoothing(demand, 0.3);
            Assert.That(result, Is.EqualTo(10.0m).Within(0.01m));
        }

        [Test]
        public void ExponentialSmoothing_TrendUpward_ShouldForecastHigher()
        {
            var demand = new List<int> { 1, 2, 3, 4, 5 };
            var result = _service.ExponentialSmoothing(demand, 0.5);
            Assert.That(result, Is.GreaterThan(3.0m));
        }

        [Test]
        public void ExponentialSmoothing_EmptyList_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.ExponentialSmoothing(new List<int>(), 0.3));
        }

        [Test]
        public void ExponentialSmoothing_NullList_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.ExponentialSmoothing(null!, 0.3));
        }

        [Test]
        public void ExponentialSmoothing_InvalidAlpha_ShouldThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.ExponentialSmoothing(new List<int> { 10 }, 0));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _service.ExponentialSmoothing(new List<int> { 10 }, 1.5));
        }

        [Test]
        public void ExponentialSmoothing_AlphaOne_ShouldReturnLastValue()
        {
            var demand = new List<int> { 5, 10, 15, 20 };
            var result = _service.ExponentialSmoothing(demand, 1.0);
            // alpha=1 means pure last value
            Assert.That(result, Is.EqualTo(20.0m));
        }

        // ── Holt Exponential Smoothing ────────────────────────────────

        [Test]
        public void HoltExponentialSmoothing_LinearTrend_ShouldForecastHigher()
        {
            var demand = new List<int> { 10, 12, 14, 16, 18 };
            var result = _service.HoltExponentialSmoothing(demand, 0.3, 0.1);
            Assert.That(result, Is.GreaterThan(16m));
        }

        [Test]
        public void HoltExponentialSmoothing_TooFewDataPoints_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.HoltExponentialSmoothing(new List<int> { 10 }));
        }

        [Test]
        public void HoltExponentialSmoothing_DeclineTrend_ShouldForecastLower()
        {
            var demand = new List<int> { 20, 18, 16, 14, 12 };
            var result = _service.HoltExponentialSmoothing(demand, 0.3, 0.1);
            Assert.That(result, Is.LessThan(14m));
        }

        // ── Seasonal Indices ──────────────────────────────────────────

        [Test]
        public void CalculateSeasonalIndices_UniformData_ShouldReturnAllOnes()
        {
            var monthly = new List<int>(
                new int[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 });
            var indices = _service.CalculateSeasonalIndices(monthly);
            Assert.That(indices.Count, Is.EqualTo(12));
            foreach (var idx in indices)
                Assert.That(idx, Is.EqualTo(1.0).Within(0.001));
        }

        [Test]
        public void CalculateSeasonalIndices_TooFewMonths_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.CalculateSeasonalIndices(new List<int> { 1, 2, 3 }));
        }

        [Test]
        public void CalculateSeasonalIndices_PeakMonth_ShouldHaveIndexAboveOne()
        {
            // Month 11 (Dec) is festive peak
            var monthly = new int[] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 50 };
            var indices = _service.CalculateSeasonalIndices(monthly);
            Assert.That(indices[11], Is.GreaterThan(1.0));
        }

        // ── Full Forecast Pipeline ────────────────────────────────────

        [Test]
        public async Task ForecastNextMonthAsync_WithHistory_ShouldReturnPositiveForecast()
        {
            SeedSalesHistory();
            var result = await _service.ForecastNextMonthAsync(1, null, periodsBack: 3);
            Assert.That(result, Is.GreaterThanOrEqualTo(0m));
        }

        [Test]
        public async Task ForecastNextMonthAsync_NoHistory_ShouldReturnZero()
        {
            var result = await _service.ForecastNextMonthAsync(99, null);
            Assert.That(result, Is.EqualTo(0m));
        }

        [Test]
        public async Task GetForecastAccuracyAsync_NoData_ShouldReturnHundred()
        {
            var accuracy = await _service.GetForecastAccuracyAsync(99);
            Assert.That(accuracy, Is.EqualTo(100m));
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
                    InvoiceNo = $"INV-{i:D3}",
                    ItemId    = 1,
                    LocationId = 1,
                    Quantity  = 5,
                    UnitPrice = 30000m,
                    TotalAmount = 150000m,
                    SaleDate  = now.AddMonths(-i)
                });
            }
            _context.SaveChanges();
        }
    }
}
