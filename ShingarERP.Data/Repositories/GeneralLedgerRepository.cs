using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for GeneralLedger with account, date, and voucher queries.</summary>
    public class GeneralLedgerRepository : GenericRepository<GeneralLedger>
    {
        public GeneralLedgerRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns GL entries for an account within a date range.</summary>
        public async Task<IEnumerable<GeneralLedger>> GetByAccountAsync(int accountId, DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(g => g.AccountId == accountId && g.PostingDate >= from && g.PostingDate <= to)
                .OrderBy(g => g.PostingDate)
                .ToListAsync(ct);

        /// <summary>Returns all GL entries for a given voucher number.</summary>
        public async Task<IEnumerable<GeneralLedger>> GetByVoucherAsync(string voucherNo, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(g => g.VoucherNo == voucherNo)
                .OrderBy(g => g.PostingDate)
                .ToListAsync(ct);

        /// <summary>Returns all GL entries within a date range.</summary>
        public async Task<IEnumerable<GeneralLedger>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(g => g.PostingDate >= from && g.PostingDate <= to)
                .OrderBy(g => g.PostingDate)
                .ToListAsync(ct);

        /// <summary>Returns the most recent GL entry for an account to get its running balance.</summary>
        public async Task<GeneralLedger?> GetLatestBalanceAsync(int accountId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(g => g.AccountId == accountId)
                .OrderByDescending(g => g.PostingDate)
                .ThenByDescending(g => g.Id)
                .FirstOrDefaultAsync(ct);
    }
}
