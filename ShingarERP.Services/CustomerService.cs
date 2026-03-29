using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.DTOs;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;
using ShingarERP.Data.Repositories;

namespace ShingarERP.Services
{
    /// <summary>
    /// Customer master and KYC service (Module 15).
    /// Handles profile CRUD, KYC verification, and LTV scoring.
    /// </summary>
    public class CustomerService
    {
        private readonly IUnitOfWork        _uow;
        private readonly CustomerRepository _customerRepo;
        private readonly ILogger<CustomerService> _logger;

        public CustomerService(
            IUnitOfWork uow,
            CustomerRepository customerRepo,
            ILogger<CustomerService> logger)
        {
            _uow          = uow;
            _customerRepo = customerRepo;
            _logger       = logger;
        }

        // ── Create / Update ──────────────────────────────────────────

        /// <summary>Create a new customer with basic profile.</summary>
        public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
        {
            // Validate mobile uniqueness
            if (await _uow.Repository<Customer>().AnyAsync(c => c.Mobile == request.Mobile, ct))
                throw new InvalidOperationException($"Customer with mobile {request.Mobile} already exists.");

            ValidateAadhaar(request.AadhaarNumber);
            ValidatePAN(request.PANNumber);

            var customer = new Customer
            {
                FirstName       = request.FirstName.Trim(),
                LastName        = request.LastName?.Trim(),
                Mobile          = request.Mobile.Trim(),
                Email           = request.Email?.Trim().ToLowerInvariant(),
                Address         = request.Address,
                City            = request.City,
                State           = request.State,
                PinCode         = request.PinCode,
                DateOfBirth     = request.DateOfBirth,
                AnniversaryDate = request.AnniversaryDate,
                Gender          = request.Gender,
                AadhaarNumber   = MaskAadhaar(request.AadhaarNumber),
                PANNumber       = request.PANNumber?.ToUpperInvariant(),
                CustomerCode    = await GenerateCustomerCodeAsync(ct)
            };

            await _uow.Repository<Customer>().AddAsync(customer, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Customer created: {Code} – {Name}", customer.CustomerCode, customer.FirstName);

            return MapCustomer(customer);
        }

        /// <summary>Update existing customer profile.</summary>
        public async Task<CustomerDto> UpdateCustomerAsync(UpdateCustomerRequest request, CancellationToken ct = default)
        {
            var customer = await _uow.Repository<Customer>().GetByIdAsync(request.CustomerId, ct)
                ?? throw new InvalidOperationException($"Customer {request.CustomerId} not found.");

            // Mobile change – check uniqueness
            if (customer.Mobile != request.Mobile &&
                await _uow.Repository<Customer>().AnyAsync(c => c.Mobile == request.Mobile && c.CustomerId != request.CustomerId, ct))
                throw new InvalidOperationException($"Mobile {request.Mobile} is already used by another customer.");

            ValidateAadhaar(request.AadhaarNumber);
            ValidatePAN(request.PANNumber);

            customer.FirstName       = request.FirstName.Trim();
            customer.LastName        = request.LastName?.Trim();
            customer.Mobile          = request.Mobile.Trim();
            customer.Email           = request.Email?.Trim().ToLowerInvariant();
            customer.Address         = request.Address;
            customer.City            = request.City;
            customer.State           = request.State;
            customer.PinCode         = request.PinCode;
            customer.DateOfBirth     = request.DateOfBirth;
            customer.AnniversaryDate = request.AnniversaryDate;
            customer.Gender          = request.Gender;
            customer.AadhaarNumber   = MaskAadhaar(request.AadhaarNumber);
            customer.PANNumber       = request.PANNumber?.ToUpperInvariant();
            customer.UpdatedAt       = DateTime.UtcNow;

            _uow.Repository<Customer>().Update(customer);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Customer updated: {Id}", customer.CustomerId);

            return MapCustomer(customer);
        }

        // ── KYC Verification ─────────────────────────────────────────

        /// <summary>Verify a customer's KYC document and mark KYC status.</summary>
        public async Task VerifyKYCAsync(KycVerificationRequest request, CancellationToken ct = default)
        {
            var customer = await _uow.Repository<Customer>().GetByIdAsync(request.CustomerId, ct)
                ?? throw new InvalidOperationException($"Customer {request.CustomerId} not found.");

            // Upsert the document
            var existing = await _uow.Repository<CustomerDocument>()
                .FirstOrDefaultAsync(d => d.CustomerId == request.CustomerId &&
                                          d.DocumentType == request.DocumentType, ct);

            if (existing != null)
            {
                existing.DocumentNumber = request.DocumentNumber;
                existing.FilePath       = request.FilePath;
                existing.IsVerified     = true;
                existing.VerifiedDate   = DateTime.UtcNow;
                _uow.Repository<CustomerDocument>().Update(existing);
            }
            else
            {
                var doc = new CustomerDocument
                {
                    CustomerId     = request.CustomerId,
                    DocumentType   = request.DocumentType,
                    DocumentNumber = request.DocumentNumber,
                    FilePath       = request.FilePath,
                    IsVerified     = true,
                    VerifiedDate   = DateTime.UtcNow
                };
                await _uow.Repository<CustomerDocument>().AddAsync(doc, ct);
            }

            // Mark customer KYC verified if Aadhaar or PAN is submitted
            if (request.DocumentType is "Aadhaar" or "PAN")
            {
                customer.KYCVerified     = true;
                customer.KYCVerifiedDate = DateTime.UtcNow;
                _uow.Repository<Customer>().Update(customer);
            }

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("KYC verified for customer {Id}, doc type: {DocType}", request.CustomerId, request.DocumentType);
        }

        // ── Search ───────────────────────────────────────────────────

        /// <summary>Search customers with pagination.</summary>
        public async Task<PagedResult<CustomerDto>> SearchCustomersAsync(CustomerSearchRequest request, CancellationToken ct = default)
        {
            var (items, total) = await _customerRepo.SearchAsync(
                request.SearchTerm, request.KYCVerified, request.IsActive,
                request.PageNumber, request.PageSize, ct);

            return new PagedResult<CustomerDto>
            {
                Items      = items.Select(MapCustomer),
                TotalCount = total,
                PageNumber = request.PageNumber,
                PageSize   = request.PageSize
            };
        }

        /// <summary>Get full customer details by ID.</summary>
        public async Task<CustomerDto?> GetCustomerByIdAsync(int customerId, CancellationToken ct = default)
        {
            var customer = await _customerRepo.GetWithDetailsAsync(customerId, ct);
            return customer == null ? null : MapCustomer(customer);
        }

        // ── LTV Score Calculation ────────────────────────────────────

        /// <summary>
        /// Recalculate LTV score for a customer.
        /// Score formula (0-1000):
        ///   - Frequency (visits/year)  : 30%
        ///   - Monetary (total spend)   : 50%
        ///   - Recency (days since last): 20%
        /// </summary>
        public async Task RecalculateLTVAsync(int customerId, CancellationToken ct = default)
        {
            var customer = await _uow.Repository<Customer>().GetByIdAsync(customerId, ct)
                ?? throw new InvalidOperationException($"Customer {customerId} not found.");

            var score = 0m;

            // Monetary: ₹10,000 = 1 point, cap 500 points
            score += Math.Min(customer.TotalPurchaseAmount / 10_000m * 1m, 500m);

            // Frequency: each purchase = 5 points, cap 300 points
            score += Math.Min(customer.TotalPurchaseCount * 5m, 300m);

            // Recency: 200 pts if purchased within 30 days, sliding to 0 at 365 days
            if (customer.LastPurchaseDate.HasValue)
            {
                var daysSince = (DateTime.UtcNow - customer.LastPurchaseDate.Value).TotalDays;
                var recency   = Math.Max(0, 200m - (decimal)daysSince / 365m * 200m);
                score += recency;
            }

            customer.LTVScore  = Math.Round(Math.Min(score, 1000m), 2);
            customer.UpdatedAt = DateTime.UtcNow;

            _uow.Repository<Customer>().Update(customer);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("LTV score recalculated for customer {Id}: {Score}", customerId, customer.LTVScore);
        }

        /// <summary>Add a family member to a customer.</summary>
        public async Task AddFamilyMemberAsync(int customerId, FamilyMember member, CancellationToken ct = default)
        {
            if (!await _uow.Repository<Customer>().AnyAsync(c => c.CustomerId == customerId, ct))
                throw new InvalidOperationException($"Customer {customerId} not found.");

            member.CustomerId = customerId;
            await _uow.Repository<FamilyMember>().AddAsync(member, ct);
            await _uow.SaveChangesAsync(ct);
        }

        // ── Private helpers ──────────────────────────────────────────

        private async Task<string> GenerateCustomerCodeAsync(CancellationToken ct)
        {
            var count = await _uow.Repository<Customer>().CountAsync(null, ct);
            return $"CUST{(count + 1):D6}";
        }

        private static string? MaskAadhaar(string? aadhaar)
        {
            if (string.IsNullOrWhiteSpace(aadhaar)) return null;
            var digits = Regex.Replace(aadhaar, @"\D", "");
            return digits.Length == 12 ? $"XXXX-XXXX-{digits[8..]}" : aadhaar;
        }

        private static void ValidateAadhaar(string? aadhaar)
        {
            if (string.IsNullOrWhiteSpace(aadhaar)) return;
            var digits = Regex.Replace(aadhaar, @"\D", "");
            if (digits.Length != 12)
                throw new ArgumentException("Aadhaar number must be 12 digits.");
        }

        private static void ValidatePAN(string? pan)
        {
            if (string.IsNullOrWhiteSpace(pan)) return;
            if (!Regex.IsMatch(pan.Trim(), @"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", RegexOptions.IgnoreCase))
                throw new ArgumentException("Invalid PAN format. Expected ABCDE1234F.");
        }

        private static CustomerDto MapCustomer(Customer c) => new()
        {
            CustomerId          = c.CustomerId,
            FirstName           = c.FirstName,
            LastName            = c.LastName,
            FullName            = c.FullName,
            Mobile              = c.Mobile,
            Email               = c.Email,
            Address             = c.Address,
            City                = c.City,
            State               = c.State,
            PinCode             = c.PinCode,
            DateOfBirth         = c.DateOfBirth,
            AnniversaryDate     = c.AnniversaryDate,
            Gender              = c.Gender,
            AadhaarNumber       = c.AadhaarNumber,
            PANNumber           = c.PANNumber,
            KYCVerified         = c.KYCVerified,
            KYCVerifiedDate     = c.KYCVerifiedDate,
            LTVScore            = c.LTVScore,
            TotalPurchaseAmount = c.TotalPurchaseAmount,
            TotalPurchaseCount  = c.TotalPurchaseCount,
            LastPurchaseDate    = c.LastPurchaseDate,
            CustomerCode        = c.CustomerCode,
            IsActive            = c.IsActive
        };
    }
}
