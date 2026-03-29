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
    public class BarcodeManagementServiceTests
    {
        private ShingarContext          _context     = null!;
        private UnitOfWork              _uow         = null!;
        private BarcodeRepository       _barcodeRepo = null!;
        private BarcodeManagementService _service    = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase($"BarcodeTest_{Guid.NewGuid()}")
                .Options;

            _context     = new ShingarContext(options);
            _uow         = new UnitOfWork(_context);
            _barcodeRepo = new BarcodeRepository(_context);
            _service     = new BarcodeManagementService(
                _uow, _barcodeRepo,
                NullLogger<BarcodeManagementService>.Instance);

            SeedTestData();
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        // ── EAN-13 Generation ─────────────────────────────────────────

        [Test]
        public void GenerateEan13_ValidInputs_ShouldReturn13Digits()
        {
            var barcode = _service.GenerateEan13(1, 1);
            Assert.That(barcode.Length, Is.EqualTo(13));
            Assert.That(barcode, Does.Match(@"^\d{13}$"));
        }

        [Test]
        public void GenerateEan13_ShouldHaveCorrectCheckDigit()
        {
            var barcode = _service.GenerateEan13(1, 1);
            Assert.That(_service.ValidateEan13(barcode), Is.True);
        }

        [Test]
        public void GenerateEan13_InvalidPrefix_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.GenerateEan13(1, 1, "AB"));
        }

        // ── EAN-13 Validation ─────────────────────────────────────────

        [Test]
        public void ValidateEan13_ValidBarcode_ShouldReturnTrue()
        {
            // Known valid EAN-13: 4006381333931 (Faber-Castell pencil)
            Assert.That(_service.ValidateEan13("4006381333931"), Is.True);
        }

        [Test]
        public void ValidateEan13_WrongLength_ShouldReturnFalse()
        {
            Assert.That(_service.ValidateEan13("12345"), Is.False);
        }

        [Test]
        public void ValidateEan13_NonNumeric_ShouldReturnFalse()
        {
            Assert.That(_service.ValidateEan13("ABCDEFGHIJKLM"), Is.False);
        }

        [Test]
        public void ValidateEan13_CorruptCheckDigit_ShouldReturnFalse()
        {
            var barcode = _service.GenerateEan13(1, 1);
            // Flip the last digit
            var corrupted = barcode.Substring(0, 12) + ((barcode[12] == '9') ? "0" : (char)(barcode[12] + 1));
            Assert.That(_service.ValidateEan13(corrupted), Is.False);
        }

        // ── QR Code Generation ────────────────────────────────────────

        [Test]
        public void GenerateQrCode_ValidInputs_ShouldReturnNonEmptyString()
        {
            var qr = _service.GenerateQrCode(1, "RNG-001", 1);
            Assert.That(qr, Is.Not.Null.And.Not.Empty);
            Assert.That(qr, Does.StartWith("QR-"));
        }

        [Test]
        public void GenerateQrCode_DifferentItems_ShouldGenerateDifferentCodes()
        {
            var qr1 = _service.GenerateQrCode(1, "RNG-001", 1);
            var qr2 = _service.GenerateQrCode(2, "RNG-002", 1);
            Assert.That(qr1, Is.Not.EqualTo(qr2));
        }

        // ── Registration & Lookup ─────────────────────────────────────

        [Test]
        public async Task RegisterBarcodeAsync_EAN13_ShouldCreateRecord()
        {
            var info = await _service.RegisterBarcodeAsync(1, 1, "EAN13");
            Assert.That(info.BarcodeValue, Is.Not.Null.And.Not.Empty);
            Assert.That(info.BarcodeType,  Is.EqualTo("EAN13"));
            Assert.That(info.IsActive,     Is.True);
        }

        [Test]
        public async Task RegisterBarcodeAsync_ExplicitValue_ShouldUseProvidedValue()
        {
            var info = await _service.RegisterBarcodeAsync(1, 1, "EAN13", "8901234567890");
            Assert.That(info.BarcodeValue, Is.EqualTo("8901234567890"));
        }

        [Test]
        public async Task RegisterBarcodeAsync_DuplicateValue_ShouldThrow()
        {
            await _service.RegisterBarcodeAsync(1, 1, "EAN13", "9999999999990");
            var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RegisterBarcodeAsync(1, 1, "EAN13", "9999999999990"));
            Assert.That(ex!.Message, Does.Contain("already exists"));
        }

        [Test]
        public async Task LookupByBarcodeAsync_ExistingBarcode_ShouldReturnInfo()
        {
            var registered = await _service.RegisterBarcodeAsync(1, 1, "EAN13", "1234567890128");
            var found = await _service.LookupByBarcodeAsync("1234567890128");
            Assert.That(found, Is.Not.Null);
            Assert.That(found!.BarcodeValue, Is.EqualTo("1234567890128"));
        }

        [Test]
        public async Task LookupByBarcodeAsync_NonExistent_ShouldReturnNull()
        {
            var found = await _service.LookupByBarcodeAsync("0000000000000");
            Assert.That(found, Is.Null);
        }

        [Test]
        public void LookupByBarcodeAsync_EmptyValue_ShouldThrow()
        {
            Assert.ThrowsAsync<ArgumentException>(() =>
                _service.LookupByBarcodeAsync(string.Empty));
        }

        // ── Integrity & Deactivation ──────────────────────────────────

        [Test]
        public async Task RunIntegrityCheckAsync_ValidBarcodes_ShouldReturnEmptyList()
        {
            await _service.RegisterBarcodeAsync(1, 1, "EAN13"); // auto-generated, valid
            var invalid = await _service.RunIntegrityCheckAsync(1);
            Assert.That(invalid, Is.Empty);
        }

        [Test]
        public async Task DeactivateBarcodesAsync_ShouldDeactivateAllForItem()
        {
            await _service.RegisterBarcodeAsync(1, 1, "EAN13");
            await _service.DeactivateBarcodesAsync(1);
            var barcodes = await _service.GetItemBarcodesAsync(1);
            Assert.That(barcodes, Is.Empty);  // GetByItemAsync only returns active
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
    }
}
