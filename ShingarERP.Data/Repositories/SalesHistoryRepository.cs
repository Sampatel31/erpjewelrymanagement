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
    /// Repository for the complete sales ledger (SalesHistory).
    /// </summary>
    public class SalesHistoryRepository
    {
        private readonly ShingarContext _context;

        /// <summary>Initialises a new instance of <see cref="SalesHistoryRepository"/>.</summary>
        public SalesHistoryRepository(ShingarContext context) => _context = context;

        /// <summary>Get sales history for an item within a date range.</summary>
        public async Task<List<SalesHistory>> GetByItemAsync(
            int itemId, DateTime from, DateTime to, CancellationToken ct = default)
            => await _context.SalesHistories
                .AsNoTracking()
                .Include(s => s.Location)
                .Where(s => s.ItemId == itemId && s.SaleDate >= from && s.SaleDate <= to)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync(ct);

        /// <summary>Get sales for a location within a date range.</summary>
        public async Task<List<SalesHistory>> GetByLocationAsync(
            int locationId, DateTime from, DateTime to, CancellationToken ct = default)
            => await _context.SalesHistories
                .AsNoTracking()
                .Include(s => s.FinishedGood)
                .Where(s => s.LocationId == locationId && s.SaleDate >= from && s.SaleDate <= to)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync(ct);

        /// <summary>Get total quantity sold for an item over a period.</summary>
        public async Task<int> GetTotalQuantitySoldAsync(
            int itemId, DateTime from, DateTime to, CancellationToken ct = default)
            => await _context.SalesHistories
                .Where(s => s.ItemId == itemId && s.SaleDate >= from && s.SaleDate <= to)
                .SumAsync(s => s.Quantity, ct);

        /// <summary>Get monthly sales totals for a date range (for forecasting input).</summary>
        public async Task<List<(int Year, int Month, int TotalQty)>> GetMonthlySalesAsync(
            int itemId, int? locationId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var query = _context.SalesHistories
                .AsNoTracking()
                .Where(s => s.ItemId == itemId && s.SaleDate >= from && s.SaleDate <= to);

            if (locationId.HasValue)
                query = query.Where(s => s.LocationId == locationId.Value);

            var raw = await query
                .Select(s => new { s.SaleDate.Year, s.SaleDate.Month, s.Quantity })
                .ToListAsync(ct);

            return raw
                .GroupBy(x => new { x.Year, x.Month })
                .Select(g => (g.Key.Year, g.Key.Month, g.Sum(x => x.Quantity)))
                .OrderBy(t => t.Year).ThenBy(t => t.Month)
                .ToList();
        }

        /// <summary>Get top-selling items for a location within a period.</summary>
        public async Task<List<(int ItemId, int TotalQty)>> GetTopSellingItemsAsync(
            int locationId, DateTime from, DateTime to, int top = 10, CancellationToken ct = default)
        {
            var raw = await _context.SalesHistories
                .AsNoTracking()
                .Where(s => s.LocationId == locationId && s.SaleDate >= from && s.SaleDate <= to)
                .Select(s => new { s.ItemId, s.Quantity })
                .ToListAsync(ct);

            return raw
                .GroupBy(x => x.ItemId)
                .Select(g => (g.Key, g.Sum(x => x.Quantity)))
                .OrderByDescending(t => t.Item2)
                .Take(top)
                .ToList();
        }
    }
}
