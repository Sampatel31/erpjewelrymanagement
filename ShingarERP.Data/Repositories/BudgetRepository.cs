using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for BudgetAllocation with fiscal year, period, account, and variance queries.</summary>
    public class BudgetRepository : GenericRepository<BudgetAllocation>
    {
        public BudgetRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all budget allocations for a given fiscal year.</summary>
        public async Task<IEnumerable<BudgetAllocation>> GetByFiscalYearAsync(int year, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(b => b.FiscalYear == year)
                .ToListAsync(ct);

        /// <summary>Returns budget allocations for a specific year and month.</summary>
        public async Task<IEnumerable<BudgetAllocation>> GetByPeriodAsync(int year, int month, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(b => b.FiscalYear == year && b.PeriodMonth == month)
                .ToListAsync(ct);

        /// <summary>Returns all budget allocations for a specific account.</summary>
        public async Task<IEnumerable<BudgetAllocation>> GetByAccountAsync(int accountId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(b => b.AccountId == accountId)
                .ToListAsync(ct);

        /// <summary>Returns allocations where actual spending exceeds the budget.</summary>
        public async Task<IEnumerable<BudgetAllocation>> GetOverBudgetAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(b => b.ActualAmount > b.BudgetedAmount)
                .ToListAsync(ct);
    }
}
