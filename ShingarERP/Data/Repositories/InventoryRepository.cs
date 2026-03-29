using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Inventory-specific queries beyond the generic repository.</summary>
    public class InventoryRepository
    {
        private readonly ShingarContext _context;

        public InventoryRepository(ShingarContext context)
            => _context = context;

        // ── Metal Lots ──────────────────────────────────────────────

        /// <summary>Get all lots with remaining stock above zero.</summary>
        public async Task<List<MetalLot>> GetActiveLotsByMetalAsync(int metalId, CancellationToken ct = default)
            => await _context.MetalLots
                .AsNoTracking()
                .Include(l => l.Supplier)
                .Include(l => l.Metal)
                .Where(l => l.MetalId == metalId && l.RemainingWeight > 0 && l.IsActive)
                .OrderByDescending(l => l.PurchaseDate)
                .ToListAsync(ct);

        /// <summary>Get total remaining weight across all lots for a metal.</summary>
        public async Task<decimal> GetTotalRemainingWeightAsync(int metalId, CancellationToken ct = default)
            => await _context.MetalLots
                .Where(l => l.MetalId == metalId && l.IsActive)
                .SumAsync(l => l.RemainingWeight, ct);

        // ── Metal Rates ─────────────────────────────────────────────

        /// <summary>Get latest rate for a metal.</summary>
        public async Task<MetalRate?> GetLatestRateAsync(int metalId, CancellationToken ct = default)
            => await _context.MetalRates
                .AsNoTracking()
                .Where(r => r.MetalId == metalId)
                .OrderByDescending(r => r.RateDate)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);

        /// <summary>Get rate history for chart display.</summary>
        public async Task<List<MetalRate>> GetRateHistoryAsync(int metalId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
            => await _context.MetalRates
                .AsNoTracking()
                .Where(r => r.MetalId == metalId && r.RateDate >= fromDate && r.RateDate <= toDate)
                .OrderBy(r => r.RateDate)
                .ToListAsync(ct);

        // ── Finished Goods ──────────────────────────────────────────

        /// <summary>Get paged finished goods with category and metal info.</summary>
        public async Task<(List<FinishedGood> Items, int TotalCount)> GetFinishedGoodsPagedAsync(
            string? search, string? location, int? categoryId, int page, int pageSize, CancellationToken ct = default)
        {
            var query = _context.FinishedGoods
                .AsNoTracking()
                .Include(f => f.Category)
                .Include(f => f.Metal)
                .Where(f => f.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(f => f.SKU.Contains(search)
                    || f.ItemName.Contains(search)
                    || (f.BarcodeNumber != null && f.BarcodeNumber.Contains(search)));

            if (!string.IsNullOrWhiteSpace(location))
                query = query.Where(f => f.StockLocation == location);

            if (categoryId.HasValue)
                query = query.Where(f => f.CategoryId == categoryId.Value);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderBy(f => f.ItemName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        /// <summary>Get items with stock below threshold (dead stock detection).</summary>
        public async Task<List<FinishedGood>> GetAgingStockAsync(int daysThreshold = 180, CancellationToken ct = default)
        {
            var cutoff = DateTime.UtcNow.AddDays(-daysThreshold);
            return await _context.FinishedGoods
                .AsNoTracking()
                .Include(f => f.Category)
                .Include(f => f.Metal)
                .Where(f => f.IsActive && f.StockQuantity > 0 && f.UpdatedAt <= cutoff)
                .OrderBy(f => f.UpdatedAt)
                .ToListAsync(ct);
        }

        // ── Stones ──────────────────────────────────────────────────

        /// <summary>Get available stones matching 4Cs filter.</summary>
        public async Task<List<Stone>> GetAvailableStonesAsync(
            string? stoneType, string? minColor, string? clarity, decimal? minCarat, decimal? maxCarat,
            CancellationToken ct = default)
        {
            var query = _context.Stones
                .AsNoTracking()
                .Where(s => s.Status == "Available" && s.IsActive);

            if (!string.IsNullOrWhiteSpace(stoneType))
                query = query.Where(s => s.StoneType == stoneType);

            if (!string.IsNullOrWhiteSpace(clarity))
                query = query.Where(s => s.Clarity == clarity);

            if (!string.IsNullOrWhiteSpace(minColor))
                query = query.Where(s => s.Color != null && string.Compare(s.Color, minColor) <= 0);

            if (minCarat.HasValue)
                query = query.Where(s => s.CaratWeight >= minCarat.Value);

            if (maxCarat.HasValue)
                query = query.Where(s => s.CaratWeight <= maxCarat.Value);

            return await query.OrderBy(s => s.StoneType).ThenBy(s => s.CaratWeight).ToListAsync(ct);
        }

        // ── Stock value ─────────────────────────────────────────────

        /// <summary>Calculate total inventory value at current rates.</summary>
        public async Task<decimal> GetFinishedGoodsTotalValueAsync(CancellationToken ct = default)
            => await _context.FinishedGoods
                .Where(f => f.IsActive && f.StockQuantity > 0)
                .SumAsync(f => f.SalePrice * f.StockQuantity, ct);
    }
}
