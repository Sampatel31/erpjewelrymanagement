using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for TrialBalance with period and account queries.</summary>
    public class TrialBalanceRepository : GenericRepository<TrialBalance>
    {
        public TrialBalanceRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns trial balance rows for a given period.</summary>
        public async Task<IEnumerable<TrialBalance>> GetByPeriodAsync(DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(tb => tb.PeriodStart == periodStart && tb.PeriodEnd == periodEnd)
                .ToListAsync(ct);

        /// <summary>Returns the most recently generated trial balance set.</summary>
        public async Task<IEnumerable<TrialBalance>> GetLatestAsync(CancellationToken ct = default)
        {
            var latest = await _dbSet.AsNoTracking()
                .OrderByDescending(tb => tb.GeneratedDate)
                .Select(tb => tb.GeneratedDate)
                .FirstOrDefaultAsync(ct);

            if (latest == default)
                return new List<TrialBalance>();

            return await _dbSet.AsNoTracking()
                .Where(tb => tb.GeneratedDate == latest)
                .ToListAsync(ct);
        }

        /// <summary>Returns all trial balance rows for a specific account.</summary>
        public async Task<IEnumerable<TrialBalance>> GetByAccountAsync(int accountId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(tb => tb.AccountId == accountId)
                .OrderByDescending(tb => tb.GeneratedDate)
                .ToListAsync(ct);
    }
}
