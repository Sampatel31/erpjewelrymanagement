using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for MeltingBatch with date, metal, and status queries.</summary>
    public class MeltingBatchRepository : GenericRepository<MeltingBatch>
    {
        public MeltingBatchRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns batches by metal type.</summary>
        public async Task<IEnumerable<MeltingBatch>> GetByMetalTypeAsync(string metalType, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(b => b.MetalType == metalType)
                .OrderByDescending(b => b.BatchDate)
                .ToListAsync(ct);

        /// <summary>Returns batches with a specific status.</summary>
        public async Task<IEnumerable<MeltingBatch>> GetByStatusAsync(string status, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(b => b.Status == status)
                .OrderByDescending(b => b.BatchDate)
                .ToListAsync(ct);

        /// <summary>Returns batches within a date range.</summary>
        public async Task<IEnumerable<MeltingBatch>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(b => b.BatchDate >= from && b.BatchDate <= to)
                .OrderBy(b => b.BatchDate)
                .ToListAsync(ct);

        /// <summary>Returns a batch with inputs and alloy compositions included.</summary>
        public async Task<MeltingBatch?> GetWithDetailsAsync(int batchId, CancellationToken ct = default)
            => await _dbSet
                .Include(b => b.Inputs)
                .Include(b => b.AlloyCompositions)
                .FirstOrDefaultAsync(b => b.Id == batchId, ct);
    }
}
