using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for Design with collection, metal, complexity, and popularity queries.</summary>
    public class DesignRepository : GenericRepository<Design>
    {
        public DesignRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns designs in a specific collection.</summary>
        public async Task<IEnumerable<Design>> GetByCollectionAsync(int collectionId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(d => d.CollectionId == collectionId && d.IsActive)
                .OrderByDescending(d => d.PopularityScore)
                .ToListAsync(ct);

        /// <summary>Returns designs by metal type.</summary>
        public async Task<IEnumerable<Design>> GetByMetalTypeAsync(string metalType, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(d => d.MetalType == metalType && d.IsActive)
                .OrderByDescending(d => d.PopularityScore)
                .ToListAsync(ct);

        /// <summary>Returns designs by complexity level.</summary>
        public async Task<IEnumerable<Design>> GetByComplexityAsync(string complexity, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(d => d.Complexity == complexity && d.IsActive)
                .ToListAsync(ct);

        /// <summary>Returns trending designs by popularity score descending.</summary>
        public async Task<IEnumerable<Design>> GetTrendingAsync(int top = 10, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(d => d.IsActive && d.Status == "Active")
                .OrderByDescending(d => d.PopularityScore)
                .Take(top)
                .ToListAsync(ct);

        /// <summary>Returns a design with BOM, photos, history, and making charges included.</summary>
        public async Task<Design?> GetWithDetailsAsync(int designId, CancellationToken ct = default)
            => await _dbSet
                .Include(d => d.BOMs)
                .Include(d => d.Photos)
                .Include(d => d.History)
                .Include(d => d.MakingCharges)
                .FirstOrDefaultAsync(d => d.Id == designId, ct);
    }
}
