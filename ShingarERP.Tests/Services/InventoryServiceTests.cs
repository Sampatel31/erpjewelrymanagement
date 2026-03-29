using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ShingarERP.Core.DTOs;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;
using ShingarERP.Services;

namespace ShingarERP.Tests.Services
{
    [TestFixture]
    public class InventoryServiceTests
    {
        private ShingarContext    _context = null!;
        private UnitOfWork        _uow     = null!;
        private InventoryRepository _inventoryRepo = null!;
        private InventoryService  _service = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase($"ShingarTest_{Guid.NewGuid()}")
                .Options;

            _context       = new ShingarContext(options);
            _uow           = new UnitOfWork(_context);
            _inventoryRepo = new InventoryRepository(_context);
            _service       = new InventoryService(_uow, _inventoryRepo, NullLogger<InventoryService>.Instance);

            SeedTestData();
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        // ── Metal Tests ──────────────────────────────────────────────

        [Test]
        public async Task GetAllMetalsAsync_ShouldReturnActiveMetals()
        {
            var metals = await _service.GetAllMetalsAsync();
            Assert.That(metals, Is.Not.Empty);
        }

        [Test]
        public async Task CreateMetalLotAsync_ValidRequest_ShouldCreateLot()
        {
            var request = new CreateMetalLotRequest
            {
                LotNumber           = "LOT-TEST-001",
                MetalId             = 1,
                SupplierId          = 1,
                GrossWeight         = 110m,
                NetWeight           = 100m,
                MeltingLossPercent  = 9.09m,
                PurchaseRatePerGram = 6500m,
                PurchaseDate        = DateTime.Today
            };

            var lot = await _service.CreateMetalLotAsync(request);

            Assert.That(lot.LotNumber,           Is.EqualTo("LOT-TEST-001"));
            Assert.That(lot.NetWeight,            Is.EqualTo(100m));
            Assert.That(lot.TotalCost,            Is.EqualTo(100m * 6500m));
            Assert.That(lot.RemainingWeight,      Is.EqualTo(100m));
        }

        [Test]
        public async Task CreateMetalLotAsync_DuplicateLotNumber_ShouldThrow()
        {
            // Create first lot
            await _service.CreateMetalLotAsync(new CreateMetalLotRequest
            {
                LotNumber           = "LOT-DUP-001",
                MetalId             = 1,
                SupplierId          = 1,
                GrossWeight         = 50m,
                NetWeight           = 48m,
                PurchaseRatePerGram = 6500m,
                PurchaseDate        = DateTime.Today
            });

            // Attempt duplicate
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateMetalLotAsync(new CreateMetalLotRequest
                {
                    LotNumber           = "LOT-DUP-001",
                    MetalId             = 1,
                    SupplierId          = 1,
                    GrossWeight         = 50m,
                    NetWeight           = 48m,
                    PurchaseRatePerGram = 6500m,
                    PurchaseDate        = DateTime.Today
                }));

            Assert.That(ex!.Message, Does.Contain("already exists"));
        }

        [Test]
        public async Task ConsumeLotWeightAsync_ValidAmount_ShouldReduceWeight()
        {
            // Create a lot first
            var lot = await _service.CreateMetalLotAsync(new CreateMetalLotRequest
            {
                LotNumber           = "LOT-CONSUME-001",
                MetalId             = 1,
                SupplierId          = 1,
                GrossWeight         = 100m,
                NetWeight           = 95m,
                PurchaseRatePerGram = 6500m,
                PurchaseDate        = DateTime.Today
            });

            await _service.ConsumeLotWeightAsync(lot.LotId, 20m, "Manufacturing");

            var updatedLot = await _uow.Repository<MetalLot>().GetByIdAsync(lot.LotId);
            Assert.That(updatedLot!.RemainingWeight, Is.EqualTo(75m));
        }

        [Test]
        public async Task ConsumeLotWeightAsync_ExceedsStock_ShouldThrow()
        {
            var lot = await _service.CreateMetalLotAsync(new CreateMetalLotRequest
            {
                LotNumber           = "LOT-OVERFLOW-001",
                MetalId             = 1,
                SupplierId          = 1,
                GrossWeight         = 50m,
                NetWeight           = 48m,
                PurchaseRatePerGram = 6500m,
                PurchaseDate        = DateTime.Today
            });

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.ConsumeLotWeightAsync(lot.LotId, 100m, "Manufacturing"));

            Assert.That(ex!.Message, Does.Contain("Insufficient weight"));
        }

        // ── Finished Good Tests ──────────────────────────────────────

        [Test]
        public async Task CreateFinishedGoodAsync_ValidRequest_ShouldCreate()
        {
            var request = new CreateFinishedGoodRequest
            {
                SKU                 = "RNG-22K-001",
                ItemName            = "Solitaire Gold Ring",
                CategoryId          = 1,
                MetalId             = 1,
                GrossWeight         = 5.5m,
                NetWeight           = 5.0m,
                MakingChargePerGram = 350m,
                SalePrice           = 38500m,
                StockLocation       = "Showroom"
            };

            var item = await _service.CreateFinishedGoodAsync(request);

            Assert.That(item.SKU,      Is.EqualTo("RNG-22K-001"));
            Assert.That(item.SalePrice, Is.EqualTo(38500m));
        }

        [Test]
        public async Task CreateFinishedGoodAsync_DuplicateSKU_ShouldThrow()
        {
            var request = new CreateFinishedGoodRequest
            {
                SKU           = "RNG-DUP-001",
                ItemName      = "Test Ring",
                CategoryId    = 1,
                MetalId       = 1,
                GrossWeight   = 5m,
                NetWeight     = 4.5m,
                SalePrice     = 30000m,
                StockLocation = "Showroom"
            };

            await _service.CreateFinishedGoodAsync(request);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateFinishedGoodAsync(request));

            Assert.That(ex!.Message, Does.Contain("already exists"));
        }

        // ── Stone Tests ──────────────────────────────────────────────

        [Test]
        public async Task RegisterStoneAsync_ValidRequest_ShouldCreate()
        {
            var request = new CreateStoneRequest
            {
                StoneCode     = "DIA-0001",
                StoneType     = "Diamond",
                CertificateNo = "GIA-12345678",
                CertLab       = "GIA",
                CaratWeight   = 0.5m,
                Color         = "F",
                Clarity       = "VS1",
                Cut           = "Excellent",
                PurchasePrice = 85000m,
                SalePrice     = 105000m
            };

            var stone = await _service.RegisterStoneAsync(request);

            Assert.That(stone.StoneCode,   Is.EqualTo("DIA-0001"));
            Assert.That(stone.CaratWeight, Is.EqualTo(0.5m));
            Assert.That(stone.Status,      Is.EqualTo("Available"));
        }

        // ── Helpers ──────────────────────────────────────────────────

        private void SeedTestData()
        {
            _context.Metals.AddRange(
                new Metal { MetalId = 1, MetalType = "Gold",   PurityCode = "22K", Fineness = 916, WeightUnit = "g" },
                new Metal { MetalId = 2, MetalType = "Silver", PurityCode = "99.9", Fineness = 999, WeightUnit = "g" }
            );
            _context.ItemCategories.Add(
                new ItemCategory { CategoryId = 1, CategoryName = "Ring", CategoryCode = "RNG" }
            );
            _context.Suppliers.Add(
                new Supplier { SupplierId = 1, SupplierName = "Test Supplier", IsActive = true }
            );
            _context.SaveChanges();
        }
    }
}
