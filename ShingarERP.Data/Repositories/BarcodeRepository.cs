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
    /// Repository for barcode lookup and validation operations.
    /// </summary>
    public class BarcodeRepository
    {
        private readonly ShingarContext _context;

        /// <summary>Initialises a new instance of <see cref="BarcodeRepository"/>.</summary>
        public BarcodeRepository(ShingarContext context) => _context = context;

        /// <summary>Look up a barcode by value.</summary>
        public async Task<BarcodeInfo?> GetByValueAsync(string barcodeValue, CancellationToken ct = default)
            => await _context.BarcodeInfos
                .AsNoTracking()
                .Include(b => b.FinishedGood)
                .Include(b => b.Location)
                .FirstOrDefaultAsync(b => b.BarcodeValue == barcodeValue && b.IsActive, ct);

        /// <summary>Get all barcodes for an item.</summary>
        public async Task<List<BarcodeInfo>> GetByItemAsync(int itemId, CancellationToken ct = default)
            => await _context.BarcodeInfos
                .AsNoTracking()
                .Where(b => b.ItemId == itemId && b.IsActive)
                .OrderByDescending(b => b.PrintedAt)
                .ToListAsync(ct);

        /// <summary>Get all barcodes printed for a location.</summary>
        public async Task<List<BarcodeInfo>> GetByLocationAsync(int locationId, CancellationToken ct = default)
            => await _context.BarcodeInfos
                .AsNoTracking()
                .Include(b => b.FinishedGood)
                .Where(b => b.LocationId == locationId && b.IsActive)
                .OrderByDescending(b => b.PrintedAt)
                .ToListAsync(ct);

        /// <summary>Check whether a barcode value already exists (including inactive).</summary>
        public async Task<bool> ExistsAsync(string barcodeValue, CancellationToken ct = default)
            => await _context.BarcodeInfos.AnyAsync(b => b.BarcodeValue == barcodeValue, ct);

        /// <summary>Deactivate all barcodes for an item (e.g., item re-labelled).</summary>
        public async Task DeactivateByItemAsync(int itemId, CancellationToken ct = default)
        {
            var barcodes = await _context.BarcodeInfos
                .Where(b => b.ItemId == itemId && b.IsActive)
                .ToListAsync(ct);

            foreach (var b in barcodes)
                b.IsActive = false;

            await _context.SaveChangesAsync(ct);
        }
    }
}
