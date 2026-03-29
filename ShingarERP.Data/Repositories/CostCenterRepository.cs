using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for CostCenter with hierarchy and type queries.</summary>
    public class CostCenterRepository : GenericRepository<CostCenter>
    {
        public CostCenterRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all active cost centres.</summary>
        public async Task<IEnumerable<CostCenter>> GetActiveAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(cc => cc.IsActive)
                .ToListAsync(ct);

        /// <summary>Returns active cost centres of a given type (e.g. Department).</summary>
        public async Task<IEnumerable<CostCenter>> GetByTypeAsync(string type, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(cc => cc.CostCenterType == type && cc.IsActive)
                .ToListAsync(ct);

        /// <summary>Returns root cost centres (those with no parent).</summary>
        public async Task<IEnumerable<CostCenter>> GetRootCentersAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(cc => cc.ParentCostCenterId == null && cc.IsActive)
                .ToListAsync(ct);

        /// <summary>Returns immediate children of a given parent cost centre.</summary>
        public async Task<IEnumerable<CostCenter>> GetChildrenAsync(int parentId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(cc => cc.ParentCostCenterId == parentId)
                .ToListAsync(ct);
    }
}
