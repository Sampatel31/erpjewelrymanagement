using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for JobCard with status, date, karigar, and stage queries.</summary>
    public class JobCardRepository : GenericRepository<JobCard>
    {
        public JobCardRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns job cards with a specific status.</summary>
        public async Task<IEnumerable<JobCard>> GetByStatusAsync(string status, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(jc => jc.Status == status)
                .OrderByDescending(jc => jc.IssuedDate)
                .ToListAsync(ct);

        /// <summary>Returns job cards assigned to a specific karigar.</summary>
        public async Task<IEnumerable<JobCard>> GetByKarigarAsync(int karigarId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(jc => jc.KarigarId == karigarId)
                .OrderByDescending(jc => jc.IssuedDate)
                .ToListAsync(ct);

        /// <summary>Returns job cards issued within a date range.</summary>
        public async Task<IEnumerable<JobCard>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(jc => jc.IssuedDate >= from && jc.IssuedDate <= to)
                .OrderBy(jc => jc.IssuedDate)
                .ToListAsync(ct);

        /// <summary>Returns a job card with all stages, history, labor, and materials included.</summary>
        public async Task<JobCard?> GetWithDetailsAsync(int jobCardId, CancellationToken ct = default)
            => await _dbSet
                .Include(jc => jc.Stages)
                .Include(jc => jc.History)
                .Include(jc => jc.Labor)
                .Include(jc => jc.Materials)
                .FirstOrDefaultAsync(jc => jc.Id == jobCardId, ct);

        /// <summary>Returns overdue job cards (past due date and not completed).</summary>
        public async Task<IEnumerable<JobCard>> GetOverdueAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(jc => jc.DueDate < DateTime.UtcNow && jc.CompletedDate == null && jc.Status != "Cancelled")
                .OrderBy(jc => jc.DueDate)
                .ToListAsync(ct);
    }
}
