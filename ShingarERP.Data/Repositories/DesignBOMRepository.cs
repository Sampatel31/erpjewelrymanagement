using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for DesignBOM (bill of materials) queries.</summary>
    public class DesignBOMRepository : GenericRepository<DesignBOM>
    {
        public DesignBOMRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all BOM items for a specific design.</summary>
        public async Task<IEnumerable<DesignBOM>> GetByDesignAsync(int designId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(b => b.DesignId == designId)
                .OrderBy(b => b.MaterialType)
                .ToListAsync(ct);

        /// <summary>Returns BOM items by material type across all designs.</summary>
        public async Task<IEnumerable<DesignBOM>> GetByMaterialTypeAsync(string materialType, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(b => b.MaterialType == materialType)
                .ToListAsync(ct);
    }
}
