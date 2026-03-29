using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for DesignPhoto asset management.</summary>
    public class DesignPhotoRepository : GenericRepository<DesignPhoto>
    {
        public DesignPhotoRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all photos for a specific design.</summary>
        public async Task<IEnumerable<DesignPhoto>> GetByDesignAsync(int designId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(p => p.DesignId == designId)
                .OrderBy(p => p.ViewAngle)
                .ToListAsync(ct);

        /// <summary>Returns the primary photo for a design.</summary>
        public async Task<DesignPhoto?> GetPrimaryPhotoAsync(int designId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(p => p.DesignId == designId && p.IsPrimary, ct);
    }
}
