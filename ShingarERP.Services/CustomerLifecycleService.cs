using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;
using ShingarERP.Data.Repositories;

namespace ShingarERP.Services
{
    /// <summary>
    /// Manages customer lifecycle including KYC verification, credit management,
    /// loyalty points, and customer segmentation for jewellery business.
    /// </summary>
    public class CustomerLifecycleService
    {
        private readonly IUnitOfWork _uow;
        private readonly CustomerKYCRepository _kycRepo;
        private readonly CustomerCreditRepository _creditRepo;
        private readonly CustomerPreferenceRepository _prefRepo;
        private readonly LoyaltyProgramRepository _loyaltyRepo;
        private readonly ILogger<CustomerLifecycleService> _logger;

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public CustomerLifecycleService(
            IUnitOfWork uow,
            CustomerKYCRepository kycRepo,
            CustomerCreditRepository creditRepo,
            CustomerPreferenceRepository prefRepo,
            LoyaltyProgramRepository loyaltyRepo,
            ILogger<CustomerLifecycleService> logger)
        {
            _uow = uow;
            _kycRepo = kycRepo;
            _creditRepo = creditRepo;
            _prefRepo = prefRepo;
            _loyaltyRepo = loyaltyRepo;
            _logger = logger;
        }

        // ── KYC ──────────────────────────────────────────────────────

        /// <summary>Initiates a KYC process for a customer. Throws if a pending/in-progress KYC already exists.</summary>
        public async Task<CustomerKYC> InitiateKYCAsync(int customerId, string? occupation = null, decimal annualIncome = 0, CancellationToken ct = default)
        {
            var existing = await _kycRepo.GetByCustomerAsync(customerId, ct);
            if (existing != null && (existing.KYCStatus == "Pending" || existing.KYCStatus == "InProgress"))
                throw new InvalidOperationException($"A KYC record already exists for customer {customerId} with status {existing.KYCStatus}.");

            var kyc = new CustomerKYC
            {
                CustomerId    = customerId,
                KYCStatus     = "Pending",
                Occupation    = occupation,
                AnnualIncome  = annualIncome,
                SubmittedDate = DateTime.UtcNow,
                CreatedAt     = DateTime.UtcNow,
                UpdatedAt     = DateTime.UtcNow
            };

            await _uow.Repository<CustomerKYC>().AddAsync(kyc, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("KYC initiated for customer {CustomerId}", customerId);
            return kyc;
        }

        /// <summary>Marks a KYC record as Verified. Throws if already verified.</summary>
        public async Task<CustomerKYC> VerifyKYCAsync(int kycId, int verifiedByUserId, CancellationToken ct = default)
        {
            var kyc = await _uow.Repository<CustomerKYC>().GetByIdAsync(kycId, ct)
                ?? throw new InvalidOperationException($"KYC record {kycId} not found.");

            if (kyc.KYCStatus == "Verified")
                throw new InvalidOperationException($"KYC record {kycId} is already verified.");

            kyc.KYCStatus        = "Verified";
            kyc.VerifiedDate     = DateTime.UtcNow;
            kyc.ExpiryDate       = DateTime.UtcNow.AddYears(1);
            kyc.VerifiedByUserId = verifiedByUserId;
            kyc.UpdatedAt        = DateTime.UtcNow;

            _uow.Repository<CustomerKYC>().Update(kyc);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("KYC {KycId} verified by user {UserId}", kycId, verifiedByUserId);
            return kyc;
        }

        /// <summary>Rejects a KYC record with a rejection reason.</summary>
        public async Task<CustomerKYC> RejectKYCAsync(int kycId, string rejectionReason, int userId, CancellationToken ct = default)
        {
            var kyc = await _uow.Repository<CustomerKYC>().GetByIdAsync(kycId, ct)
                ?? throw new InvalidOperationException($"KYC record {kycId} not found.");

            kyc.KYCStatus        = "Rejected";
            kyc.RejectionReason  = rejectionReason;
            kyc.VerifiedByUserId = userId;
            kyc.UpdatedAt        = DateTime.UtcNow;

            _uow.Repository<CustomerKYC>().Update(kyc);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("KYC {KycId} rejected", kycId);
            return kyc;
        }

        /// <summary>Returns the current KYC record for a customer, or null if none exists.</summary>
        public async Task<CustomerKYC?> GetKYCStatusAsync(int customerId, CancellationToken ct = default)
            => await _kycRepo.GetByCustomerAsync(customerId, ct);

        // ── Credit ────────────────────────────────────────────────────

        /// <summary>Creates or updates the credit limit for a customer.</summary>
        public async Task<CustomerCreditLimit> SetCreditLimitAsync(int customerId, decimal creditLimit, int reviewedByUserId, CancellationToken ct = default)
        {
            var existing = await _creditRepo.GetByCustomerAsync(customerId, ct);

            if (existing != null)
            {
                // Fetch tracked entity for update
                var tracked = await _uow.Repository<CustomerCreditLimit>().GetByIdAsync(existing.Id, ct)!;
                tracked!.CreditLimit       = creditLimit;
                tracked.LastReviewDate     = DateTime.UtcNow;
                tracked.NextReviewDate     = DateTime.UtcNow.AddMonths(6);
                tracked.ReviewedByUserId   = reviewedByUserId;
                tracked.UpdatedAt          = DateTime.UtcNow;

                _uow.Repository<CustomerCreditLimit>().Update(tracked);
                await _uow.SaveChangesAsync(ct);
                return tracked;
            }

            var limit = new CustomerCreditLimit
            {
                CustomerId       = customerId,
                CreditLimit      = creditLimit,
                UtilizedAmount   = 0,
                LastReviewDate   = DateTime.UtcNow,
                NextReviewDate   = DateTime.UtcNow.AddMonths(6),
                ReviewedByUserId = reviewedByUserId,
                IsActive         = true,
                CreatedAt        = DateTime.UtcNow,
                UpdatedAt        = DateTime.UtcNow
            };

            await _uow.Repository<CustomerCreditLimit>().AddAsync(limit, ct);
            await _uow.SaveChangesAsync(ct);
            return limit;
        }

        /// <summary>Adjusts credit utilization. Throws if utilization would exceed the credit limit.</summary>
        public async Task<CustomerCreditLimit> AdjustCreditUtilizationAsync(int customerId, decimal amount, bool isIncrease, CancellationToken ct = default)
        {
            var existing = await _creditRepo.GetByCustomerAsync(customerId, ct)
                ?? throw new InvalidOperationException($"No credit limit found for customer {customerId}.");

            var tracked = await _uow.Repository<CustomerCreditLimit>().GetByIdAsync(existing.Id, ct)!;

            if (isIncrease && tracked!.UtilizedAmount + amount > tracked.CreditLimit)
                throw new InvalidOperationException($"Utilization of {tracked.UtilizedAmount + amount} exceeds credit limit of {tracked.CreditLimit}.");

            tracked!.UtilizedAmount = isIncrease
                ? tracked.UtilizedAmount + amount
                : Math.Max(0, tracked.UtilizedAmount - amount);
            tracked.UpdatedAt = DateTime.UtcNow;

            _uow.Repository<CustomerCreditLimit>().Update(tracked);
            await _uow.SaveChangesAsync(ct);
            return tracked;
        }

        /// <summary>Returns the available credit balance for a customer.</summary>
        public async Task<decimal> GetAvailableCreditAsync(int customerId, CancellationToken ct = default)
        {
            var limit = await _creditRepo.GetByCustomerAsync(customerId, ct);
            return limit?.AvailableCredit ?? 0;
        }

        // ── Preferences ───────────────────────────────────────────────

        /// <summary>Records a new customer preference entry.</summary>
        public async Task<CustomerPreference> RecordPreferenceAsync(
            int customerId,
            string? metalType,
            string? designStyle,
            decimal minPrice,
            decimal maxPrice,
            string? occasion,
            CancellationToken ct = default)
        {
            var pref = new CustomerPreference
            {
                CustomerId        = customerId,
                MetalType         = metalType,
                DesignStyle       = designStyle,
                MinPriceRange     = minPrice,
                MaxPriceRange     = maxPrice,
                PreferredOccasion = occasion,
                RecordedAt        = DateTime.UtcNow
            };

            await _uow.Repository<CustomerPreference>().AddAsync(pref, ct);
            await _uow.SaveChangesAsync(ct);
            return pref;
        }

        /// <summary>Returns all recorded preferences for a customer.</summary>
        public async Task<IEnumerable<CustomerPreference>> GetCustomerPreferencesAsync(int customerId, CancellationToken ct = default)
            => await _prefRepo.GetByCustomerAsync(customerId, ct);

        // ── Loyalty ───────────────────────────────────────────────────

        /// <summary>Enrolls a customer in the loyalty programme. Throws if already enrolled.</summary>
        public async Task<LoyaltyProgram> EnrollInLoyaltyAsync(int customerId, CancellationToken ct = default)
        {
            var existing = await _loyaltyRepo.GetByCustomerAsync(customerId, ct);
            if (existing != null)
                throw new InvalidOperationException($"Customer {customerId} is already enrolled in the loyalty programme.");

            var program = new LoyaltyProgram
            {
                CustomerId    = customerId,
                CurrentPoints = 0,
                LifetimePoints = 0,
                CurrentTier   = "Bronze",
                IsActive      = true,
                EnrolledAt    = DateTime.UtcNow,
                UpdatedAt     = DateTime.UtcNow
            };

            await _uow.Repository<LoyaltyProgram>().AddAsync(program, ct);
            await _uow.SaveChangesAsync(ct);
            return program;
        }

        /// <summary>Adds earned points to a customer's loyalty account and checks for tier upgrade.</summary>
        public async Task<LoyaltyTransaction> EarnPointsAsync(int customerId, int points, string? referenceType, int? referenceId, string? description, CancellationToken ct = default)
        {
            var program = await _loyaltyRepo.GetByCustomerAsync(customerId, ct)
                ?? throw new InvalidOperationException($"Customer {customerId} is not enrolled in the loyalty programme.");

            var tracked = await _uow.Repository<LoyaltyProgram>().GetByIdAsync(program.Id, ct)!;

            tracked!.CurrentPoints         += points;
            tracked.LifetimePoints          += points;
            tracked.LastPointsEarnedDate    = DateTime.UtcNow;
            tracked.UpdatedAt              = DateTime.UtcNow;

            var newTier = await CalculateTierAsync(tracked.LifetimePoints);
            if (newTier != tracked.CurrentTier)
            {
                tracked.TierUpgradeDate = DateTime.UtcNow;
                tracked.CurrentTier     = newTier;
            }

            var txn = new LoyaltyTransaction
            {
                LoyaltyProgramId  = tracked.Id,
                TransactionType   = "Earn",
                Points            = points,
                BalanceAfter      = tracked.CurrentPoints,
                ReferenceType     = referenceType,
                ReferenceId       = referenceId,
                Description       = description,
                TransactionDate   = DateTime.UtcNow,
                CreatedAt         = DateTime.UtcNow
            };

            _uow.Repository<LoyaltyProgram>().Update(tracked);
            await _uow.Repository<LoyaltyTransaction>().AddAsync(txn, ct);
            await _uow.SaveChangesAsync(ct);
            return txn;
        }

        /// <summary>Redeems points from a customer's loyalty account. Throws if balance is insufficient.</summary>
        public async Task<LoyaltyTransaction> RedeemPointsAsync(int customerId, int points, string? referenceType, int? referenceId, CancellationToken ct = default)
        {
            var program = await _loyaltyRepo.GetByCustomerAsync(customerId, ct)
                ?? throw new InvalidOperationException($"Customer {customerId} is not enrolled in the loyalty programme.");

            if (program.CurrentPoints < points)
                throw new InvalidOperationException($"Insufficient loyalty points. Available: {program.CurrentPoints}, requested: {points}.");

            var tracked = await _uow.Repository<LoyaltyProgram>().GetByIdAsync(program.Id, ct)!;
            tracked!.CurrentPoints -= points;
            tracked.UpdatedAt = DateTime.UtcNow;

            var txn = new LoyaltyTransaction
            {
                LoyaltyProgramId = tracked.Id,
                TransactionType  = "Redeem",
                Points           = -points,
                BalanceAfter     = tracked.CurrentPoints,
                ReferenceType    = referenceType,
                ReferenceId      = referenceId,
                TransactionDate  = DateTime.UtcNow,
                CreatedAt        = DateTime.UtcNow
            };

            _uow.Repository<LoyaltyProgram>().Update(tracked);
            await _uow.Repository<LoyaltyTransaction>().AddAsync(txn, ct);
            await _uow.SaveChangesAsync(ct);
            return txn;
        }

        /// <summary>Returns the current loyalty points balance for a customer.</summary>
        public async Task<int> GetLoyaltyBalanceAsync(int customerId, CancellationToken ct = default)
        {
            var program = await _loyaltyRepo.GetByCustomerAsync(customerId, ct);
            return program?.CurrentPoints ?? 0;
        }

        /// <summary>Returns the current loyalty tier for a customer.</summary>
        public async Task<string> GetLoyaltyTierAsync(int customerId, CancellationToken ct = default)
        {
            var program = await _loyaltyRepo.GetByCustomerAsync(customerId, ct);
            return program?.CurrentTier ?? "Bronze";
        }

        /// <summary>Calculates the tier name based on lifetime points.</summary>
        /// <remarks>Bronze &lt;1000, Silver 1000–4999, Gold 5000–9999, Platinum 10000+.</remarks>
        public Task<string> CalculateTierAsync(int lifetimePoints, CancellationToken ct = default)
        {
            string tier = lifetimePoints >= 10000 ? "Platinum"
                        : lifetimePoints >= 5000  ? "Gold"
                        : lifetimePoints >= 1000  ? "Silver"
                        : "Bronze";

            return Task.FromResult(tier);
        }

        // ── Interactions ──────────────────────────────────────────────

        /// <summary>Records a new customer interaction (call, visit, email, etc.).</summary>
        public async Task<CustomerInteraction> RecordInteractionAsync(
            int customerId,
            string interactionType,
            string subject,
            string? notes,
            int? staffUserId,
            CancellationToken ct = default)
        {
            var interaction = new CustomerInteraction
            {
                CustomerId       = customerId,
                InteractionType  = interactionType,
                Subject          = subject,
                Notes            = notes,
                StaffUserId      = staffUserId,
                InteractionDate  = DateTime.UtcNow,
                CreatedAt        = DateTime.UtcNow
            };

            await _uow.Repository<CustomerInteraction>().AddAsync(interaction, ct);
            await _uow.SaveChangesAsync(ct);
            return interaction;
        }

        /// <summary>Returns the customer segment label based on their loyalty tier.</summary>
        public async Task<string> GetCustomerSegmentAsync(int customerId, CancellationToken ct = default)
        {
            var tier = await GetLoyaltyTierAsync(customerId, ct);
            return tier switch
            {
                "Platinum" or "Gold" => "VIP",
                "Silver"             => "Regular",
                _                    => "New"
            };
        }

        /// <summary>
        /// Calculates a churn risk score (0–100) based on days since last interaction.
        /// Higher score indicates higher risk of churn.
        /// </summary>
        public async Task<decimal> CalculateChurnScoreAsync(int customerId, CancellationToken ct = default)
        {
            var interactions = await _uow.Repository<CustomerInteraction>()
                .FindAsync(i => i.CustomerId == customerId, ct);

            var lastInteraction = interactions
                .OrderByDescending(i => i.InteractionDate)
                .FirstOrDefault();

            if (lastInteraction == null)
                return 100m;

            var daysSince = (decimal)(DateTime.UtcNow - lastInteraction.InteractionDate).TotalDays;
            return Math.Min(100m, daysSince / 365m * 100m);
        }

        /// <summary>Returns interactions for a customer within a date range.</summary>
        public async Task<IEnumerable<CustomerInteraction>> GetCustomerInteractionsAsync(int customerId, DateTime from, DateTime to, CancellationToken ct = default)
            => await _uow.Repository<CustomerInteraction>()
                .FindAsync(i => i.CustomerId == customerId && i.InteractionDate >= from && i.InteractionDate <= to, ct);
    }
}
