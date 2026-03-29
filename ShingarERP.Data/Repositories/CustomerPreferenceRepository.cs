using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for CustomerPreference with metal and recency queries.</summary>
    public class CustomerPreferenceRepository : GenericRepository<CustomerPreference>
    {
        public CustomerPreferenceRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all preferences recorded for a customer.</summary>
        public async Task<IEnumerable<CustomerPreference>> GetByCustomerAsync(int customerId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(p => p.CustomerId == customerId)
                .OrderByDescending(p => p.RecordedAt)
                .ToListAsync(ct);

        /// <summary>Returns all preferences for a given metal type.</summary>
        public async Task<IEnumerable<CustomerPreference>> GetByMetalTypeAsync(string metalType, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(p => p.MetalType == metalType)
                .ToListAsync(ct);

        /// <summary>Returns the most recent preferences for a customer (default: last 10).</summary>
        public async Task<IEnumerable<CustomerPreference>> GetRecentPreferencesAsync(int customerId, int count = 10, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(p => p.CustomerId == customerId)
                .OrderByDescending(p => p.RecordedAt)
                .Take(count)
                .ToListAsync(ct);
    }
}
