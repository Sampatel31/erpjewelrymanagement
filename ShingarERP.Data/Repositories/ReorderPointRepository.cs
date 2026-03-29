using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>
    /// Repository for dynamic reorder threshold management.
    /// </summary>
    public class ReorderPointRepository
    {
        private readonly ShingarContext _context;

        /// <summary>Initialises a new instance of <see cref="ReorderPointRepository"/>.</summary>
        public ReorderPointRepository(ShingarContext context) => _context = context;

        /// <summary>Get the reorder point for an item, optionally at a specific location.</summary>
        public async Task<ReorderPoint?> GetAsync(int itemId, int? locationId, CancellationToken ct = default)
            => await _context.ReorderPoints
                .AsNoTracking()
                .FirstOrDefaultAsync(rp => rp.ItemId == itemId && rp.LocationId == locationId, ct);

        /// <summary>Get all reorder points for a location.</summary>
        public async Task<List<ReorderPoint>> GetByLocationAsync(int locationId, CancellationToken ct = default)
            => await _context.ReorderPoints
                .AsNoTracking()
                .Include(rp => rp.FinishedGood)
                .Where(rp => rp.LocationId == locationId)
                .OrderBy(rp => rp.AbcCategory)
                .ThenBy(rp => rp.FinishedGood.ItemName)
                .ToListAsync(ct);

        /// <summary>Get all "A" category (high-value) reorder points.</summary>
        public async Task<List<ReorderPoint>> GetAbcCategoryAsync(string category, CancellationToken ct = default)
            => await _context.ReorderPoints
                .AsNoTracking()
                .Include(rp => rp.FinishedGood)
                .Include(rp => rp.Location)
                .Where(rp => rp.AbcCategory == category)
                .ToListAsync(ct);

        /// <summary>Upsert a reorder point record.</summary>
        public async Task UpsertAsync(ReorderPoint record, CancellationToken ct = default)
        {
            var existing = await _context.ReorderPoints
                .FirstOrDefaultAsync(rp => rp.ItemId == record.ItemId && rp.LocationId == record.LocationId, ct);

            if (existing == null)
                _context.ReorderPoints.Add(record);
            else
            {
                existing.ReorderLevel    = record.ReorderLevel;
                existing.OrderQuantity   = record.OrderQuantity;
                existing.SafetyStock     = record.SafetyStock;
                existing.LeadTimeDays    = record.LeadTimeDays;
                existing.AbcCategory     = record.AbcCategory;
                existing.LastCalculated  = DateTime.UtcNow;
                existing.UpdatedAt       = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
        }

        /// <summary>Get all items that currently need replenishment at a given location.</summary>
        public async Task<List<(ReorderPoint Rp, int CurrentQty)>> GetItemsNeedingReplenishmentAsync(
            int locationId, CancellationToken ct = default)
        {
            var reorderPoints = await GetByLocationAsync(locationId, ct);
            var result = new List<(ReorderPoint, int)>();

            foreach (var rp in reorderPoints)
            {
                var li = await _context.LocationInventories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.LocationId == locationId && x.ItemId == rp.ItemId, ct);
                var qty = li?.Quantity ?? 0;
                if (qty <= rp.ReorderLevel)
                    result.Add((rp, qty));
            }

            return result;
        }
    }
}
