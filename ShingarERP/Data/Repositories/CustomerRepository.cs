using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Customer-specific repository queries.</summary>
    public class CustomerRepository
    {
        private readonly ShingarContext _context;

        public CustomerRepository(ShingarContext context) => _context = context;

        /// <summary>Find customer by mobile number.</summary>
        public async Task<Customer?> GetByMobileAsync(string mobile, CancellationToken ct = default)
            => await _context.Customers
                .AsNoTracking()
                .Include(c => c.Documents)
                .Include(c => c.FamilyMembers)
                .FirstOrDefaultAsync(c => c.Mobile == mobile, ct);

        /// <summary>Search customers by name, mobile, or customer code.</summary>
        public async Task<(List<Customer> Items, int TotalCount)> SearchAsync(
            string? searchTerm,
            bool?   kycVerified,
            bool?   isActive,
            int     page,
            int     pageSize,
            CancellationToken ct = default)
        {
            var query = _context.Customers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(c =>
                    c.FirstName.Contains(term) ||
                    (c.LastName != null && c.LastName.Contains(term)) ||
                    c.Mobile.Contains(term) ||
                    (c.CustomerCode != null && c.CustomerCode.Contains(term)) ||
                    (c.AadhaarNumber != null && c.AadhaarNumber.Contains(term)) ||
                    (c.PANNumber != null && c.PANNumber.Contains(term)));
            }

            if (kycVerified.HasValue)
                query = query.Where(c => c.KYCVerified == kycVerified.Value);

            if (isActive.HasValue)
                query = query.Where(c => c.IsActive == isActive.Value);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(c => c.FirstName)
                .ThenBy(c => c.LastName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        /// <summary>Get top customers by LTV score.</summary>
        public async Task<List<Customer>> GetTopCustomersAsync(int count = 10, CancellationToken ct = default)
            => await _context.Customers
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.LTVScore)
                .Take(count)
                .ToListAsync(ct);

        /// <summary>Get customers with birthdays this month (for campaign targeting).</summary>
        public async Task<List<Customer>> GetBirthdayCustomersAsync(int month, CancellationToken ct = default)
            => await _context.Customers
                .AsNoTracking()
                .Where(c => c.IsActive && c.DateOfBirth.HasValue && c.DateOfBirth.Value.Month == month)
                .OrderBy(c => c.DateOfBirth!.Value.Day)
                .ToListAsync(ct);

        /// <summary>Get full customer profile with all related entities.</summary>
        public async Task<Customer?> GetWithDetailsAsync(int customerId, CancellationToken ct = default)
            => await _context.Customers
                .Include(c => c.Documents)
                .Include(c => c.FamilyMembers)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
    }
}
