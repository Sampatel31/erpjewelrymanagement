using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for SubContractor with skill, performance, and pending delivery queries.</summary>
    public class SubContractorRepository : GenericRepository<SubContractor>
    {
        public SubContractorRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns active sub-contractors with a specific skill (partial match).</summary>
        public async Task<IEnumerable<SubContractor>> GetBySkillAsync(string skill, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(sc => sc.IsActive && sc.Skills != null && sc.Skills.Contains(skill))
                .OrderByDescending(sc => sc.PerformanceScore)
                .ToListAsync(ct);

        /// <summary>Returns top-performing sub-contractors by performance score.</summary>
        public async Task<IEnumerable<SubContractor>> GetTopPerformersAsync(int top = 10, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(sc => sc.IsActive)
                .OrderByDescending(sc => sc.PerformanceScore)
                .Take(top)
                .ToListAsync(ct);

        /// <summary>Returns a sub-contractor with all challans included.</summary>
        public async Task<SubContractor?> GetWithChallansAsync(int subContractorId, CancellationToken ct = default)
            => await _dbSet
                .Include(sc => sc.Challans)
                .FirstOrDefaultAsync(sc => sc.Id == subContractorId, ct);
    }
}
