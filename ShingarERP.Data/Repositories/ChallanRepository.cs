using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for Challan with status, date, sub-contractor, and overdue queries.</summary>
    public class ChallanRepository : GenericRepository<Challan>
    {
        public ChallanRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns challans by status.</summary>
        public async Task<IEnumerable<Challan>> GetByStatusAsync(string status, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(c => c.Status == status)
                .OrderByDescending(c => c.ChallanDate)
                .ToListAsync(ct);

        /// <summary>Returns challans for a specific sub-contractor.</summary>
        public async Task<IEnumerable<Challan>> GetBySubContractorAsync(int subContractorId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(c => c.SubContractorId == subContractorId)
                .OrderByDescending(c => c.ChallanDate)
                .ToListAsync(ct);

        /// <summary>Returns challans issued within a date range.</summary>
        public async Task<IEnumerable<Challan>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(c => c.ChallanDate >= from && c.ChallanDate <= to)
                .OrderBy(c => c.ChallanDate)
                .ToListAsync(ct);

        /// <summary>Returns overdue challans (past due date, not accepted or paid).</summary>
        public async Task<IEnumerable<Challan>> GetOverdueAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(c => c.DueDate < DateTime.UtcNow
                    && c.Status != "Accepted" && c.Status != "Paid" && c.Status != "Rejected")
                .OrderBy(c => c.DueDate)
                .ToListAsync(ct);

        /// <summary>Returns a challan with lines, receivals, and payments included.</summary>
        public async Task<Challan?> GetWithDetailsAsync(int challanId, CancellationToken ct = default)
            => await _dbSet
                .Include(c => c.Lines)
                .Include(c => c.Receivals)
                .Include(c => c.Payments)
                .FirstOrDefaultAsync(c => c.Id == challanId, ct);
    }
}
