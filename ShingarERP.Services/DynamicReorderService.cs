using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;
using ShingarERP.Data.Repositories;

namespace ShingarERP.Services
{
    /// <summary>
    /// Provides intelligent reorder management including ABC analysis,
    /// safety stock calculations, lead-time adjustment, and EOQ computation.
    /// </summary>
    public class DynamicReorderService
    {
        /// <summary>
        /// Multiplier used to estimate the total catalogue value when only a single item's
        /// annual sales value is known. Assumes the item contributes approximately 10% of
        /// the catalogue's total annual turnover — a conservative heuristic for ABC seeding.
        /// Replace with an actual catalogue-wide sum for production accuracy.
        /// </summary>
        private const decimal SingleItemTotalValueMultiplier = 10m;
        private readonly IUnitOfWork             _uow;
        private readonly ReorderPointRepository  _reorderRepo;
        private readonly SalesHistoryRepository  _salesHistoryRepo;
        private readonly ILogger<DynamicReorderService> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="DynamicReorderService"/>.
        /// </summary>
        public DynamicReorderService(
            IUnitOfWork uow,
            ReorderPointRepository reorderRepo,
            SalesHistoryRepository salesHistoryRepo,
            ILogger<DynamicReorderService> logger)
        {
            _uow              = uow;
            _reorderRepo      = reorderRepo;
            _salesHistoryRepo = salesHistoryRepo;
            _logger           = logger;
        }

        // ── ABC Analysis ─────────────────────────────────────────────

        /// <summary>
        /// Classify an item as A, B, or C using annual sales value.
        /// A = top 20% of value (approx. 80% revenue), B = next 30%, C = remaining 50%.
        /// </summary>
        /// <param name="annualSalesValue">Annual revenue contribution of the item.</param>
        /// <param name="totalInventoryValue">Total annual revenue of the entire inventory.</param>
        /// <returns>ABC category string: "A", "B", or "C".</returns>
        public string ClassifyAbc(decimal annualSalesValue, decimal totalInventoryValue)
        {
            if (totalInventoryValue <= 0) return "C";

            var share = annualSalesValue / totalInventoryValue;
            return share >= 0.10m ? "A"   // individual item ≥ 10% of total = A
                 : share >= 0.03m ? "B"   // 3–10% = B
                 : "C";                   // < 3% = C
        }

        // ── Economic Order Quantity ───────────────────────────────────

        /// <summary>
        /// Calculate Economic Order Quantity (EOQ) using Wilson's formula.
        /// EOQ = √(2 × D × S / H)
        /// </summary>
        /// <param name="annualDemand">Annual demand in units.</param>
        /// <param name="orderingCost">Fixed cost per order (₹).</param>
        /// <param name="holdingCostPerUnit">Annual holding cost per unit (₹).</param>
        /// <returns>Optimal order quantity (integer, minimum 1).</returns>
        public int CalculateEoq(int annualDemand, decimal orderingCost, decimal holdingCostPerUnit)
        {
            if (annualDemand <= 0)     throw new ArgumentOutOfRangeException(nameof(annualDemand), "Must be positive.");
            if (orderingCost <= 0)     throw new ArgumentOutOfRangeException(nameof(orderingCost), "Must be positive.");
            if (holdingCostPerUnit <= 0) throw new ArgumentOutOfRangeException(nameof(holdingCostPerUnit), "Must be positive.");

            var eoq = Math.Sqrt(
                (double)(2m * annualDemand * orderingCost) /
                (double)holdingCostPerUnit);

            return Math.Max(1, (int)Math.Round(eoq));
        }

        // ── Safety Stock ──────────────────────────────────────────────

        /// <summary>
        /// Calculate safety stock as a multiple of the standard deviation of demand
        /// scaled by lead time.  Uses a Z-score of 1.65 for ~95% service level.
        /// </summary>
        /// <param name="avgDailyDemand">Average daily demand (units/day).</param>
        /// <param name="stdDevDailyDemand">Standard deviation of daily demand.</param>
        /// <param name="leadTimeDays">Supplier lead time in days.</param>
        /// <param name="zScore">Service level Z-score (1.65 = 95%, 2.33 = 99%).</param>
        /// <returns>Safety stock quantity (integer, minimum 0).</returns>
        public int CalculateSafetyStock(
            double avgDailyDemand, double stdDevDailyDemand, int leadTimeDays, double zScore = 1.65)
        {
            if (leadTimeDays < 0) throw new ArgumentOutOfRangeException(nameof(leadTimeDays), "Lead time cannot be negative.");

            var safetyStock = zScore * stdDevDailyDemand * Math.Sqrt(leadTimeDays);
            return Math.Max(0, (int)Math.Ceiling(safetyStock));
        }

        // ── Reorder Point ─────────────────────────────────────────────

        /// <summary>
        /// Calculate the reorder point: demand during lead time + safety stock.
        /// </summary>
        public int CalculateReorderLevel(double avgDailyDemand, int leadTimeDays, int safetyStock)
            => (int)Math.Ceiling(avgDailyDemand * leadTimeDays) + safetyStock;

        // ── Dynamic Calculation & Persistence ────────────────────────

        /// <summary>
        /// Fully recalculate and persist the reorder point for an item at a location.
        /// Uses actual sales history for demand statistics.
        /// </summary>
        public async Task<ReorderPoint> RecalculateAsync(
            int itemId, int? locationId, int leadTimeDays,
            decimal orderingCost = 500m, decimal holdingCostPerUnit = 200m,
            CancellationToken ct = default)
        {
            // Pull last 90 days of daily sales
            var to   = DateTime.UtcNow;
            var from = to.AddDays(-90);

            var monthly = await _salesHistoryRepo.GetMonthlySalesAsync(itemId, locationId, from, to, ct);
            var totalQty   = monthly.Sum(m => m.TotalQty);
            var monthCount = Math.Max(1, monthly.Count);

            double avgMonthlyDemand = (double)totalQty / monthCount;
            double avgDailyDemand   = avgMonthlyDemand / 30.0;

            // Simple std dev from monthly data
            double variance = monthly.Count > 1
                ? monthly.Sum(m => Math.Pow((double)m.TotalQty - avgMonthlyDemand, 2)) / (monthly.Count - 1)
                : 0;
            double stdDevMonthly = Math.Sqrt(variance);
            double stdDevDaily   = stdDevMonthly / Math.Sqrt(30.0);

            int safetyStock   = CalculateSafetyStock(avgDailyDemand, stdDevDaily, leadTimeDays);
            int reorderLevel  = CalculateReorderLevel(avgDailyDemand, leadTimeDays, safetyStock);

            int annualDemand  = (int)Math.Ceiling(avgMonthlyDemand * 12);
            int eoq = annualDemand > 0
                ? CalculateEoq(annualDemand, orderingCost, holdingCostPerUnit)
                : 1;

            // ABC classification
            var item = await _uow.Repository<FinishedGood>().GetByIdAsync(itemId, ct);
            string abc = "C";
            if (item != null)
            {
                var annualSalesValue = item.SalePrice * annualDemand;
                // Approximate total via a conservative heuristic (see SingleItemTotalValueMultiplier)
                decimal totalValue = annualSalesValue > 0 ? annualSalesValue * SingleItemTotalValueMultiplier : 1m;
                abc = ClassifyAbc(annualSalesValue, totalValue);
            }

            var rp = new ReorderPoint
            {
                ItemId          = itemId,
                LocationId      = locationId,
                ReorderLevel    = reorderLevel,
                OrderQuantity   = eoq,
                SafetyStock     = safetyStock,
                LeadTimeDays    = leadTimeDays,
                AbcCategory     = abc,
                LastCalculated  = DateTime.UtcNow
            };

            await _reorderRepo.UpsertAsync(rp, ct);

            _logger.LogInformation(
                "Reorder point recalculated for item {ItemId}: ROP={ROP}, EOQ={EOQ}, Safety={Safety}, ABC={ABC}.",
                itemId, reorderLevel, eoq, safetyStock, abc);

            return rp;
        }

        /// <summary>Get items currently needing replenishment at a location.</summary>
        public async Task<List<(ReorderPoint Rp, int CurrentQty)>> GetReplenishmentListAsync(
            int locationId, CancellationToken ct = default)
            => await _reorderRepo.GetItemsNeedingReplenishmentAsync(locationId, ct);

        /// <summary>Get all A-category items (high-value, high-attention).</summary>
        public async Task<List<ReorderPoint>> GetAItemsAsync(CancellationToken ct = default)
            => await _reorderRepo.GetAbcCategoryAsync("A", ct);

        /// <summary>Get all reorder points for a location.</summary>
        public async Task<List<ReorderPoint>> GetReorderPointsByLocationAsync(
            int locationId, CancellationToken ct = default)
            => await _reorderRepo.GetByLocationAsync(locationId, ct);
    }
}
