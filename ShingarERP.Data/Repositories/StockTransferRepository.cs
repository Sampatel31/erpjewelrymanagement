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
    /// Repository for stock transfer history and tracking.
    /// </summary>
    public class StockTransferRepository
    {
        private readonly ShingarContext _context;

        /// <summary>Initialises a new instance of <see cref="StockTransferRepository"/>.</summary>
        public StockTransferRepository(ShingarContext context) => _context = context;

        /// <summary>Get transfer by transfer number.</summary>
        public async Task<StockTransfer?> GetByTransferNoAsync(string transferNo, CancellationToken ct = default)
            => await _context.StockTransfers
                .AsNoTracking()
                .Include(t => t.FromLocation)
                .Include(t => t.ToLocation)
                .Include(t => t.FinishedGood)
                .FirstOrDefaultAsync(t => t.TransferNo == transferNo, ct);

        /// <summary>Get all transfers for a specific item.</summary>
        public async Task<List<StockTransfer>> GetByItemAsync(int itemId, CancellationToken ct = default)
            => await _context.StockTransfers
                .AsNoTracking()
                .Include(t => t.FromLocation)
                .Include(t => t.ToLocation)
                .Where(t => t.ItemId == itemId)
                .OrderByDescending(t => t.TransferDate)
                .ToListAsync(ct);

        /// <summary>Get pending transfers for a location (as source or destination).</summary>
        public async Task<List<StockTransfer>> GetPendingByLocationAsync(int locationId, CancellationToken ct = default)
            => await _context.StockTransfers
                .AsNoTracking()
                .Include(t => t.FromLocation)
                .Include(t => t.ToLocation)
                .Include(t => t.FinishedGood)
                .Where(t => (t.FromLocationId == locationId || t.ToLocationId == locationId)
                         && t.Status == "Pending")
                .OrderBy(t => t.TransferDate)
                .ToListAsync(ct);

        /// <summary>Get transfer history within a date range.</summary>
        public async Task<List<StockTransfer>> GetByDateRangeAsync(
            DateTime from, DateTime to, CancellationToken ct = default)
            => await _context.StockTransfers
                .AsNoTracking()
                .Include(t => t.FromLocation)
                .Include(t => t.ToLocation)
                .Include(t => t.FinishedGood)
                .Where(t => t.TransferDate >= from && t.TransferDate <= to)
                .OrderByDescending(t => t.TransferDate)
                .ToListAsync(ct);

        /// <summary>Check whether a transfer number already exists.</summary>
        public async Task<bool> ExistsAsync(string transferNo, CancellationToken ct = default)
            => await _context.StockTransfers.AnyAsync(t => t.TransferNo == transferNo, ct);
    }
}
