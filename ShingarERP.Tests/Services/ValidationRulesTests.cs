using System;
using FluentValidation;
using NUnit.Framework;
using ShingarERP.Core.DTOs;
using ShingarERP.Utilities;

namespace ShingarERP.Tests.Services
{
    [TestFixture]
    public class ValidationRulesTests
    {
        private CreateMetalLotValidator  _lotValidator     = null!;
        private CreateCustomerValidator  _customerValidator = null!;
        private CreateFinishedGoodValidator _goodValidator = null!;
        private CreateStoneValidator     _stoneValidator   = null!;

        [SetUp]
        public void SetUp()
        {
            _lotValidator      = new CreateMetalLotValidator();
            _customerValidator = new CreateCustomerValidator();
            _goodValidator     = new CreateFinishedGoodValidator();
            _stoneValidator    = new CreateStoneValidator();
        }

        // ── MetalLot validation ──────────────────────────────────────

        [Test]
        public void MetalLotValidator_ValidRequest_ShouldPass()
        {
            var req = new CreateMetalLotRequest
            {
                LotNumber           = "LOT-2024-001",
                MetalId             = 1,
                SupplierId          = 1,
                GrossWeight         = 110m,
                NetWeight           = 100m,
                MeltingLossPercent  = 9m,
                PurchaseRatePerGram = 6500m,
                PurchaseDate        = DateTime.Today
            };

            var result = _lotValidator.Validate(req);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void MetalLotValidator_NetWeightExceedsGross_ShouldFail()
        {
            var req = new CreateMetalLotRequest
            {
                LotNumber           = "LOT-001",
                MetalId             = 1,
                SupplierId          = 1,
                GrossWeight         = 50m,
                NetWeight           = 60m,    // > gross
                PurchaseRatePerGram = 6500m,
                PurchaseDate        = DateTime.Today
            };

            var result = _lotValidator.Validate(req);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Property("PropertyName").EqualTo("NetWeight"));
        }

        [Test]
        public void MetalLotValidator_FuturePurchaseDate_ShouldFail()
        {
            var req = new CreateMetalLotRequest
            {
                LotNumber           = "LOT-001",
                MetalId             = 1,
                SupplierId          = 1,
                GrossWeight         = 100m,
                NetWeight           = 95m,
                PurchaseRatePerGram = 6500m,
                PurchaseDate        = DateTime.Today.AddDays(5)
            };

            var result = _lotValidator.Validate(req);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void MetalLotValidator_EmptyLotNumber_ShouldFail()
        {
            var req = new CreateMetalLotRequest
            {
                LotNumber           = "",
                MetalId             = 1,
                SupplierId          = 1,
                GrossWeight         = 100m,
                NetWeight           = 95m,
                PurchaseRatePerGram = 6500m,
                PurchaseDate        = DateTime.Today
            };

            var result = _lotValidator.Validate(req);
            Assert.That(result.IsValid, Is.False);
        }

        // ── Customer validation ──────────────────────────────────────

        [Test]
        public void CustomerValidator_ValidRequest_ShouldPass()
        {
            var req = new CreateCustomerRequest
            {
                FirstName = "Ramesh",
                LastName  = "Patel",
                Mobile    = "9876543210",
                City      = "Surat",
                State     = "Gujarat"
            };

            var result = _customerValidator.Validate(req);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void CustomerValidator_InvalidMobile_ShouldFail()
        {
            var req = new CreateCustomerRequest
            {
                FirstName = "Test",
                Mobile    = "12345"    // invalid
            };

            var result = _customerValidator.Validate(req);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void CustomerValidator_InvalidPAN_ShouldFail()
        {
            var req = new CreateCustomerRequest
            {
                FirstName = "Test",
                Mobile    = "9876543210",
                PANNumber = "INVALID123"
            };

            var result = _customerValidator.Validate(req);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void CustomerValidator_ValidPAN_ShouldPass()
        {
            var req = new CreateCustomerRequest
            {
                FirstName = "Test",
                Mobile    = "9876543210",
                PANNumber = "ABCDE1234F"
            };

            var result = _customerValidator.Validate(req);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void CustomerValidator_InvalidAadhaar_ShouldFail()
        {
            var req = new CreateCustomerRequest
            {
                FirstName     = "Test",
                Mobile        = "9876543210",
                AadhaarNumber = "123"    // too short
            };

            var result = _customerValidator.Validate(req);
            Assert.That(result.IsValid, Is.False);
        }

        // ── Finished Good validation ─────────────────────────────────

        [Test]
        public void FinishedGoodValidator_ValidRequest_ShouldPass()
        {
            var req = new CreateFinishedGoodRequest
            {
                SKU           = "RNG-22K-001",
                ItemName      = "Gold Ring",
                CategoryId    = 1,
                MetalId       = 1,
                GrossWeight   = 5.5m,
                NetWeight     = 5.0m,
                SalePrice     = 35000m,
                StockLocation = "Showroom"
            };

            var result = _goodValidator.Validate(req);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void FinishedGoodValidator_InvalidSKU_ShouldFail()
        {
            var req = new CreateFinishedGoodRequest
            {
                SKU           = "rng test 001",  // lowercase with spaces
                ItemName      = "Gold Ring",
                CategoryId    = 1,
                MetalId       = 1,
                GrossWeight   = 5.5m,
                NetWeight     = 5.0m,
                SalePrice     = 35000m,
                StockLocation = "Showroom"
            };

            var result = _goodValidator.Validate(req);
            Assert.That(result.IsValid, Is.False);
        }

        // ── Stone validation ─────────────────────────────────────────

        [Test]
        public void StoneValidator_ValidRequest_ShouldPass()
        {
            var req = new CreateStoneRequest
            {
                StoneCode     = "DIA-0001",
                StoneType     = "Diamond",
                CaratWeight   = 0.5m,
                PurchasePrice = 85000m,
                SalePrice     = 105000m,
                CertLab       = "GIA",
                Cut           = "Excellent"
            };

            var result = _stoneValidator.Validate(req);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void StoneValidator_SalePriceLessThanPurchase_ShouldFail()
        {
            var req = new CreateStoneRequest
            {
                StoneCode     = "DIA-0002",
                StoneType     = "Diamond",
                CaratWeight   = 0.5m,
                PurchasePrice = 100_000m,
                SalePrice     = 50_000m    // less than purchase
            };

            var result = _stoneValidator.Validate(req);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void StoneValidator_InvalidCertLab_ShouldFail()
        {
            var req = new CreateStoneRequest
            {
                StoneCode     = "DIA-0003",
                StoneType     = "Diamond",
                CaratWeight   = 0.5m,
                PurchasePrice = 100_000m,
                SalePrice     = 120_000m,
                CertLab       = "UNKNOWN_LAB"
            };

            var result = _stoneValidator.Validate(req);
            Assert.That(result.IsValid, Is.False);
        }
    }
}
