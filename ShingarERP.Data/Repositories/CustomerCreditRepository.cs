using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for CustomerCreditLimit with utilization and review queries.</summary>
    public class CustomerCreditRepository : GenericRepository<CustomerCreditLimit>
    {
        public CustomerCreditRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns the credit limit record for a specific customer.</summary>
        public async Task<CustomerCreditLimit?> GetByCustomerAsync(int customerId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

        /// <summary>Returns credit limits with utilization above 80%.</summary>
        public async Task<IEnumerable<CustomerCreditLimit>> GetHighUtilizationAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(c => c.IsActive && c.CreditLimit > 0 && c.UtilizedAmount / c.CreditLimit * 100 > 80)
                .ToListAsync(ct);

        /// <summary>Returns active credit limits due for review (NextReviewDate &lt;= now).</summary>
        public async Task<IEnumerable<CustomerCreditLimit>> GetReviewDueAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return await _dbSet.AsNoTracking()
                .Where(c => c.IsActive && c.NextReviewDate <= now)
                .ToListAsync(ct);
        }

        /// <summary>Returns all active credit limit records.</summary>
        public async Task<IEnumerable<CustomerCreditLimit>> GetActiveLimitsAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(c => c.IsActive)
                .ToListAsync(ct);
    }
}
