using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for QCRecord with pass/fail, date, and defect analysis queries.</summary>
    public class QCRecordRepository : GenericRepository<QCRecord>
    {
        public QCRecordRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns QC records by result (Pass/Fail/Conditional).</summary>
        public async Task<IEnumerable<QCRecord>> GetByResultAsync(string result, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(q => q.Result == result)
                .OrderByDescending(q => q.InspectionDate)
                .ToListAsync(ct);

        /// <summary>Returns QC records within an inspection date range.</summary>
        public async Task<IEnumerable<QCRecord>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(q => q.InspectionDate >= from && q.InspectionDate <= to)
                .OrderBy(q => q.InspectionDate)
                .ToListAsync(ct);

        /// <summary>Returns QC records for a specific job card.</summary>
        public async Task<IEnumerable<QCRecord>> GetByJobCardAsync(int jobCardId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(q => q.JobCardId == jobCardId)
                .OrderByDescending(q => q.InspectionDate)
                .ToListAsync(ct);

        /// <summary>Returns a QC record with its defects included.</summary>
        public async Task<QCRecord?> GetWithDefectsAsync(int qcRecordId, CancellationToken ct = default)
            => await _dbSet
                .Include(q => q.Defects)
                .FirstOrDefaultAsync(q => q.Id == qcRecordId, ct);
    }
}
