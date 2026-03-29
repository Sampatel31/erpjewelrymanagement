using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for Karigar (craftsman) with skill, availability, and performance queries.</summary>
    public class KarigarRepository : GenericRepository<Karigar>
    {
        public KarigarRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all active karigars with a specific availability status.</summary>
        public async Task<IEnumerable<Karigar>> GetByAvailabilityAsync(string status, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(k => k.AvailabilityStatus == status && k.IsActive)
                .OrderBy(k => k.Name)
                .ToListAsync(ct);

        /// <summary>Returns karigars with a specific skill (by skill name).</summary>
        public async Task<IEnumerable<Karigar>> GetBySkillAsync(string skillName, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Include(k => k.Skills)
                .Where(k => k.IsActive && k.Skills.Any(s => s.SkillName == skillName))
                .OrderByDescending(k => k.PerformanceRating)
                .ToListAsync(ct);

        /// <summary>Returns top-performing karigars ordered by performance rating descending.</summary>
        public async Task<IEnumerable<Karigar>> GetTopPerformersAsync(int top = 10, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(k => k.IsActive)
                .OrderByDescending(k => k.PerformanceRating)
                .Take(top)
                .ToListAsync(ct);

        /// <summary>Returns a karigar with all skills and recent performance records included.</summary>
        public async Task<Karigar?> GetWithDetailsAsync(int karigarId, CancellationToken ct = default)
            => await _dbSet
                .Include(k => k.Skills)
                .Include(k => k.Performances)
                .FirstOrDefaultAsync(k => k.Id == karigarId, ct);
    }
}
