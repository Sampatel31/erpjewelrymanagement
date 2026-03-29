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
    public class CustomerLifecycleServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private CustomerKYCRepository _kycRepo = null!;
        private CustomerCreditRepository _creditRepo = null!;
        private CustomerPreferenceRepository _prefRepo = null!;
        private LoyaltyProgramRepository _loyaltyRepo = null!;
        private CustomerLifecycleService _service = null!;

        private int _customerId;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context     = new ShingarContext(options);
            _uow         = new UnitOfWork(_context);
            _kycRepo     = new CustomerKYCRepository(_context);
            _creditRepo  = new CustomerCreditRepository(_context);
            _prefRepo    = new CustomerPreferenceRepository(_context);
            _loyaltyRepo = new LoyaltyProgramRepository(_context);
            _service     = new CustomerLifecycleService(
                _uow, _kycRepo, _creditRepo, _prefRepo, _loyaltyRepo,
                NullLogger<CustomerLifecycleService>.Instance);

            SeedTestData();
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        private void SeedTestData()
        {
            var customer = new Customer
            {
                FirstName = "Ramesh",
                LastName  = "Patel",
                Mobile    = "9876543210",
                IsActive  = true
            };
            _context.Customers.Add(customer);
            _context.SaveChanges();
            _customerId = customer.CustomerId;
        }

        // ── KYC Tests ─────────────────────────────────────────────────

        [Test]
        public async Task InitiateKYC_Valid()
        {
            var kyc = await _service.InitiateKYCAsync(_customerId, "Jeweller", 500000);
            Assert.That(kyc.KYCStatus, Is.EqualTo("Pending"));
            Assert.That(kyc.CustomerId, Is.EqualTo(_customerId));
            Assert.That(kyc.Occupation, Is.EqualTo("Jeweller"));
        }

        [Test]
        public async Task InitiateKYC_DuplicatePending()
        {
            await _service.InitiateKYCAsync(_customerId, "Jeweller", 500000);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.InitiateKYCAsync(_customerId, "Jeweller", 500000));
        }

        [Test]
        public async Task VerifyKYC_Valid()
        {
            var kyc = await _service.InitiateKYCAsync(_customerId);
            var verified = await _service.VerifyKYCAsync(kyc.Id, 1);
            Assert.That(verified.KYCStatus, Is.EqualTo("Verified"));
            Assert.That(verified.VerifiedDate, Is.Not.Null);
            Assert.That(verified.ExpiryDate, Is.Not.Null);
        }

        [Test]
        public async Task VerifyKYC_AlreadyVerified()
        {
            var kyc = await _service.InitiateKYCAsync(_customerId);
            await _service.VerifyKYCAsync(kyc.Id, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.VerifyKYCAsync(kyc.Id, 1));
        }

        [Test]
        public async Task RejectKYC_Valid()
        {
            var kyc = await _service.InitiateKYCAsync(_customerId);
            var rejected = await _service.RejectKYCAsync(kyc.Id, "Invalid documents", 1);
            Assert.That(rejected.KYCStatus, Is.EqualTo("Rejected"));
            Assert.That(rejected.RejectionReason, Is.EqualTo("Invalid documents"));
        }

        [Test]
        public async Task GetKYCStatus_ExistingCustomer()
        {
            await _service.InitiateKYCAsync(_customerId);
            var kyc = await _service.GetKYCStatusAsync(_customerId);
            Assert.That(kyc, Is.Not.Null);
            Assert.That(kyc!.CustomerId, Is.EqualTo(_customerId));
        }

        [Test]
        public async Task GetKYCStatus_NoKYC()
        {
            var kyc = await _service.GetKYCStatusAsync(999);
            Assert.That(kyc, Is.Null);
        }

        // ── Credit Tests ──────────────────────────────────────────────

        [Test]
        public async Task SetCreditLimit_New()
        {
            var limit = await _service.SetCreditLimitAsync(_customerId, 100000, 1);
            Assert.That(limit.CreditLimit, Is.EqualTo(100000));
            Assert.That(limit.UtilizedAmount, Is.EqualTo(0));
        }

        [Test]
        public async Task SetCreditLimit_Update()
        {
            await _service.SetCreditLimitAsync(_customerId, 100000, 1);
            var updated = await _service.SetCreditLimitAsync(_customerId, 200000, 1);
            Assert.That(updated.CreditLimit, Is.EqualTo(200000));
        }

        [Test]
        public async Task AdjustCreditUp_Valid()
        {
            await _service.SetCreditLimitAsync(_customerId, 100000, 1);
            var result = await _service.AdjustCreditUtilizationAsync(_customerId, 30000, true);
            Assert.That(result.UtilizedAmount, Is.EqualTo(30000));
        }

        [Test]
        public async Task AdjustCreditExceeds_ShouldThrow()
        {
            await _service.SetCreditLimitAsync(_customerId, 50000, 1);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AdjustCreditUtilizationAsync(_customerId, 60000, true));
        }

        [Test]
        public async Task AdjustCreditDown_Valid()
        {
            await _service.SetCreditLimitAsync(_customerId, 100000, 1);
            await _service.AdjustCreditUtilizationAsync(_customerId, 40000, true);
            var result = await _service.AdjustCreditUtilizationAsync(_customerId, 10000, false);
            Assert.That(result.UtilizedAmount, Is.EqualTo(30000));
        }

        [Test]
        public async Task GetAvailableCredit_Valid()
        {
            await _service.SetCreditLimitAsync(_customerId, 100000, 1);
            await _service.AdjustCreditUtilizationAsync(_customerId, 25000, true);
            var avail = await _service.GetAvailableCreditAsync(_customerId);
            Assert.That(avail, Is.EqualTo(75000));
        }

        // ── Preference Tests ──────────────────────────────────────────

        [Test]
        public async Task RecordPreference_Valid()
        {
            var pref = await _service.RecordPreferenceAsync(_customerId, "Gold", "Modern", 10000, 50000, "Wedding");
            Assert.That(pref.MetalType, Is.EqualTo("Gold"));
            Assert.That(pref.PreferredOccasion, Is.EqualTo("Wedding"));
        }

        [Test]
        public async Task GetPreferences_Multiple()
        {
            await _service.RecordPreferenceAsync(_customerId, "Gold", "Modern", 10000, 50000, "Wedding");
            await _service.RecordPreferenceAsync(_customerId, "Silver", "Traditional", 5000, 20000, "Daily");
            var prefs = (await _service.GetCustomerPreferencesAsync(_customerId)).ToList();
            Assert.That(prefs.Count, Is.EqualTo(2));
        }

        // ── Loyalty Tests ─────────────────────────────────────────────

        [Test]
        public async Task EnrollLoyalty_Valid()
        {
            var program = await _service.EnrollInLoyaltyAsync(_customerId);
            Assert.That(program.CurrentTier, Is.EqualTo("Bronze"));
            Assert.That(program.CurrentPoints, Is.EqualTo(0));
        }

        [Test]
        public async Task EnrollLoyalty_DuplicateShouldThrow()
        {
            await _service.EnrollInLoyaltyAsync(_customerId);
            Assert.ThrowsAsync<InvalidOperationException>(() => _service.EnrollInLoyaltyAsync(_customerId));
        }

        [Test]
        public async Task EarnPoints_UpdatesBalance()
        {
            await _service.EnrollInLoyaltyAsync(_customerId);
            var txn = await _service.EarnPointsAsync(_customerId, 500, "SalesOrder", 1, "Purchase reward");
            Assert.That(txn.Points, Is.EqualTo(500));
            Assert.That(txn.BalanceAfter, Is.EqualTo(500));
        }

        [Test]
        public async Task EarnPoints_TriggersTierUpgrade()
        {
            await _service.EnrollInLoyaltyAsync(_customerId);
            await _service.EarnPointsAsync(_customerId, 1000, null, null, null);
            var tier = await _service.GetLoyaltyTierAsync(_customerId);
            Assert.That(tier, Is.EqualTo("Silver"));
        }

        [Test]
        public async Task RedeemPoints_Valid()
        {
            await _service.EnrollInLoyaltyAsync(_customerId);
            await _service.EarnPointsAsync(_customerId, 200, null, null, null);
            var txn = await _service.RedeemPointsAsync(_customerId, 100, "Invoice", 1);
            Assert.That(txn.Points, Is.EqualTo(-100));
            Assert.That(txn.BalanceAfter, Is.EqualTo(100));
        }

        [Test]
        public async Task RedeemPoints_Insufficient_ShouldThrow()
        {
            await _service.EnrollInLoyaltyAsync(_customerId);
            await _service.EarnPointsAsync(_customerId, 50, null, null, null);
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RedeemPointsAsync(_customerId, 200, null, null));
        }

        [Test]
        public async Task GetLoyaltyBalance()
        {
            await _service.EnrollInLoyaltyAsync(_customerId);
            await _service.EarnPointsAsync(_customerId, 300, null, null, null);
            var balance = await _service.GetLoyaltyBalanceAsync(_customerId);
            Assert.That(balance, Is.EqualTo(300));
        }

        [Test]
        public async Task GetLoyaltyTier()
        {
            await _service.EnrollInLoyaltyAsync(_customerId);
            var tier = await _service.GetLoyaltyTierAsync(_customerId);
            Assert.That(tier, Is.EqualTo("Bronze"));
        }

        // ── Interaction Tests ─────────────────────────────────────────

        [Test]
        public async Task RecordInteraction_Valid()
        {
            var interaction = await _service.RecordInteractionAsync(_customerId, "Call", "Follow-up call", "Discussed new arrivals", 10);
            Assert.That(interaction.InteractionType, Is.EqualTo("Call"));
            Assert.That(interaction.Subject, Is.EqualTo("Follow-up call"));
        }

        [Test]
        public async Task GetCustomerSegment_VIP()
        {
            await _service.EnrollInLoyaltyAsync(_customerId);
            await _service.EarnPointsAsync(_customerId, 5000, null, null, null);
            var segment = await _service.GetCustomerSegmentAsync(_customerId);
            Assert.That(segment, Is.EqualTo("VIP"));
        }

        [Test]
        public async Task GetCustomerSegment_New()
        {
            await _service.EnrollInLoyaltyAsync(_customerId);
            var segment = await _service.GetCustomerSegmentAsync(_customerId);
            Assert.That(segment, Is.EqualTo("New"));
        }

        [Test]
        public async Task CalculateChurnScore()
        {
            var score = await _service.CalculateChurnScoreAsync(_customerId);
            // No interactions – should be 100
            Assert.That(score, Is.EqualTo(100m));
        }

        [Test]
        public async Task GetInteractions_ByDateRange()
        {
            await _service.RecordInteractionAsync(_customerId, "Visit", "Showroom visit", null, 5);
            var from = DateTime.UtcNow.AddDays(-1);
            var to   = DateTime.UtcNow.AddDays(1);
            var list = (await _service.GetCustomerInteractionsAsync(_customerId, from, to)).ToList();
            Assert.That(list.Count, Is.GreaterThanOrEqualTo(1));
        }

        // ── Tier Calculation Tests ────────────────────────────────────

        [Test]
        public async Task CalculateTier_Bronze()
        {
            var tier = await _service.CalculateTierAsync(999);
            Assert.That(tier, Is.EqualTo("Bronze"));
        }

        [Test]
        public async Task CalculateTier_Silver()
        {
            var tier = await _service.CalculateTierAsync(1000);
            Assert.That(tier, Is.EqualTo("Silver"));
        }

        [Test]
        public async Task CalculateTier_Gold()
        {
            var tier = await _service.CalculateTierAsync(5000);
            Assert.That(tier, Is.EqualTo("Gold"));
        }

        [Test]
        public async Task CalculateTier_Platinum()
        {
            var tier = await _service.CalculateTierAsync(10000);
            Assert.That(tier, Is.EqualTo("Platinum"));
        }
    }
}
