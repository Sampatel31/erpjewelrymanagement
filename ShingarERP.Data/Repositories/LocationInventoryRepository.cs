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
    /// Specialised repository for multi-location inventory queries.
    /// </summary>
    public class LocationInventoryRepository
    {
        private readonly ShingarContext _context;

        /// <summary>Initialises a new instance of <see cref="LocationInventoryRepository"/>.</summary>
        public LocationInventoryRepository(ShingarContext context) => _context = context;

        /// <summary>Get or create the inventory record for an item at a location.</summary>
        public async Task<LocationInventory> GetOrCreateAsync(int locationId, int itemId, CancellationToken ct = default)
        {
            var entry = await _context.LocationInventories
                .FirstOrDefaultAsync(li => li.LocationId == locationId && li.ItemId == itemId, ct);

            if (entry == null)
            {
                entry = new LocationInventory
                {
                    LocationId  = locationId,
                    ItemId      = itemId,
                    Quantity    = 0,
                    LastUpdated = DateTime.UtcNow
                };
                _context.LocationInventories.Add(entry);
                await _context.SaveChangesAsync(ct);
            }

            return entry;
        }

        /// <summary>Get all inventory records for a specific location.</summary>
        public async Task<List<LocationInventory>> GetByLocationAsync(int locationId, CancellationToken ct = default)
            => await _context.LocationInventories
                .AsNoTracking()
                .Include(li => li.FinishedGood)
                .Include(li => li.Location)
                .Where(li => li.LocationId == locationId)
                .OrderBy(li => li.FinishedGood.ItemName)
                .ToListAsync(ct);

        /// <summary>Get all inventory records for a specific item across all locations.</summary>
        public async Task<List<LocationInventory>> GetByItemAsync(int itemId, CancellationToken ct = default)
            => await _context.LocationInventories
                .AsNoTracking()
                .Include(li => li.Location)
                .Where(li => li.ItemId == itemId)
                .OrderBy(li => li.Location.LocationName)
                .ToListAsync(ct);

        /// <summary>Get total quantity for an item across all active locations.</summary>
        public async Task<int> GetTotalQuantityAsync(int itemId, CancellationToken ct = default)
            => await _context.LocationInventories
                .Where(li => li.ItemId == itemId)
                .SumAsync(li => li.Quantity, ct);

        /// <summary>Get items below reorder level at a given location.</summary>
        public async Task<List<LocationInventory>> GetBelowReorderLevelAsync(int locationId, CancellationToken ct = default)
        {
            var locationInventories = await _context.LocationInventories
                .AsNoTracking()
                .Include(li => li.FinishedGood)
                .Where(li => li.LocationId == locationId)
                .ToListAsync(ct);

            var reorderPoints = await _context.ReorderPoints
                .AsNoTracking()
                .Where(rp => rp.LocationId == locationId)
                .ToDictionaryAsync(rp => rp.ItemId, ct);

            return locationInventories
                .Where(li => reorderPoints.TryGetValue(li.ItemId, out var rp) && li.Quantity <= rp.ReorderLevel)
                .ToList();
        }

        /// <summary>Get all active locations with their inventory summary.</summary>
        public async Task<List<(InventoryLocation Location, int TotalItems, int TotalQuantity)>> GetLocationSummaryAsync(
            CancellationToken ct = default)
        {
            var locations = await _context.InventoryLocations
                .AsNoTracking()
                .Where(l => l.IsActive)
                .ToListAsync(ct);

            var result = new List<(InventoryLocation, int, int)>();
            foreach (var loc in locations)
            {
                var inv = await _context.LocationInventories
                    .Where(li => li.LocationId == loc.LocationId && li.Quantity > 0)
                    .ToListAsync(ct);
                result.Add((loc, inv.Count, inv.Sum(li => li.Quantity)));
            }

            return result;
        }
    }
}
