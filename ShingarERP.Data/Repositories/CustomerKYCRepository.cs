using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for CustomerKYC with verification status queries.</summary>
    public class CustomerKYCRepository : GenericRepository<CustomerKYC>
    {
        public CustomerKYCRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all KYC records with Verified status.</summary>
        public async Task<IEnumerable<CustomerKYC>> GetVerifiedAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(k => k.KYCStatus == "Verified")
                .ToListAsync(ct);

        /// <summary>Returns all KYC records with Pending or InProgress status.</summary>
        public async Task<IEnumerable<CustomerKYC>> GetPendingAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(k => k.KYCStatus == "Pending" || k.KYCStatus == "InProgress")
                .ToListAsync(ct);

        /// <summary>Returns all KYC records that have expired (ExpiryDate in the past).</summary>
        public async Task<IEnumerable<CustomerKYC>> GetExpiredAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return await _dbSet.AsNoTracking()
                .Where(k => k.ExpiryDate.HasValue && k.ExpiryDate.Value < now)
                .ToListAsync(ct);
        }

        /// <summary>Returns the KYC record for a specific customer.</summary>
        public async Task<CustomerKYC?> GetByCustomerAsync(int customerId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(k => k.CustomerId == customerId, ct);
    }
}
