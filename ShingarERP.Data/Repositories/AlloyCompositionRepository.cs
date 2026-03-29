using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for AlloyComposition with batch and metal type queries.</summary>
    public class AlloyCompositionRepository : GenericRepository<AlloyComposition>
    {
        public AlloyCompositionRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all alloy compositions for a melting batch.</summary>
        public async Task<IEnumerable<AlloyComposition>> GetByBatchAsync(int batchId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(ac => ac.MeltingBatchId == batchId)
                .OrderBy(ac => ac.MetalType)
                .ToListAsync(ct);

        /// <summary>Returns all compositions containing a specific metal type.</summary>
        public async Task<IEnumerable<AlloyComposition>> GetByMetalTypeAsync(string metalType, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(ac => ac.MetalType == metalType)
                .ToListAsync(ct);
    }
}
