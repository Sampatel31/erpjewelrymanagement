using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for LoyaltyProgram with tier and points queries.</summary>
    public class LoyaltyProgramRepository : GenericRepository<LoyaltyProgram>
    {
        public LoyaltyProgramRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns the loyalty programme for a specific customer.</summary>
        public async Task<LoyaltyProgram?> GetByCustomerAsync(int customerId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Include(lp => lp.Transactions)
                .FirstOrDefaultAsync(lp => lp.CustomerId == customerId, ct);

        /// <summary>Returns all programmes at a given tier (e.g. Gold).</summary>
        public async Task<IEnumerable<LoyaltyProgram>> GetByTierAsync(string tier, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(lp => lp.CurrentTier == tier && lp.IsActive)
                .ToListAsync(ct);

        /// <summary>Returns top N programmes by current points balance.</summary>
        public async Task<IEnumerable<LoyaltyProgram>> GetTopPointsCustomersAsync(int count, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(lp => lp.IsActive)
                .OrderByDescending(lp => lp.CurrentPoints)
                .Take(count)
                .ToListAsync(ct);

        /// <summary>Returns programmes whose lifetime points have reached or exceeded the upgrade threshold.</summary>
        public async Task<IEnumerable<LoyaltyProgram>> GetProgramsForUpgradeAsync(int pointsThreshold, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(lp => lp.IsActive && lp.LifetimePoints >= pointsThreshold)
                .ToListAsync(ct);

        /// <summary>Returns all active loyalty programmes.</summary>
        public async Task<IEnumerable<LoyaltyProgram>> GetActiveAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(lp => lp.IsActive)
                .ToListAsync(ct);
    }
}
