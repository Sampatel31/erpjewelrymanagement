using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for QCChecklist template management.</summary>
    public class QCChecklistRepository : GenericRepository<QCChecklist>
    {
        public QCChecklistRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns active checklists for a specific item category.</summary>
        public async Task<IEnumerable<QCChecklist>> GetByCategoryAsync(int categoryId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(c => c.ItemCategoryId == categoryId && c.IsActive)
                .ToListAsync(ct);

        /// <summary>Returns a checklist with its items included.</summary>
        public async Task<QCChecklist?> GetWithItemsAsync(int checklistId, CancellationToken ct = default)
            => await _dbSet
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == checklistId, ct);
    }
}
