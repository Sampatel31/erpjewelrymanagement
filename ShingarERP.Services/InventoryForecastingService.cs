using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Models;
using ShingarERP.Data.Repositories;

namespace ShingarERP.Services
{
    /// <summary>
    /// Demand forecasting service using exponential smoothing with seasonal
    /// trend analysis to predict future inventory requirements.
    /// </summary>
    public class InventoryForecastingService
    {
        private readonly ForecastDataRepository  _forecastRepo;
        private readonly SalesHistoryRepository  _salesHistoryRepo;
        private readonly ILogger<InventoryForecastingService> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="InventoryForecastingService"/>.
        /// </summary>
        public InventoryForecastingService(
            ForecastDataRepository forecastRepo,
            SalesHistoryRepository salesHistoryRepo,
            ILogger<InventoryForecastingService> logger)
        {
            _forecastRepo     = forecastRepo;
            _salesHistoryRepo = salesHistoryRepo;
            _logger           = logger;
        }

        // ── Exponential Smoothing ─────────────────────────────────────

        /// <summary>
        /// Calculate the exponential smoothing forecast for the next period.
        /// α (alpha) controls how much weight is placed on recent data (0 &lt; α ≤ 1).
        /// </summary>
        /// <param name="historicalDemand">Ordered list of past period quantities.</param>
        /// <param name="alpha">Smoothing factor (default 0.3).</param>
        /// <returns>Forecasted quantity for the next period.</returns>
        public decimal ExponentialSmoothing(IList<int> historicalDemand, double alpha = 0.3)
        {
            if (historicalDemand == null || !historicalDemand.Any())
                throw new ArgumentException("Historical demand data cannot be empty.", nameof(historicalDemand));

            if (alpha <= 0 || alpha > 1)
                throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be between 0 (exclusive) and 1.");

            double forecast = historicalDemand[0];
            foreach (var actual in historicalDemand)
                forecast = alpha * actual + (1 - alpha) * forecast;

            return Math.Round((decimal)forecast, 4);
        }

        /// <summary>
        /// Apply Holt's double exponential smoothing (trend-adjusted).
        /// </summary>
        /// <param name="historicalDemand">Ordered list of past period quantities.</param>
        /// <param name="alpha">Level smoothing factor.</param>
        /// <param name="beta">Trend smoothing factor.</param>
        /// <returns>Forecasted quantity for the next period including trend.</returns>
        public decimal HoltExponentialSmoothing(IList<int> historicalDemand, double alpha = 0.3, double beta = 0.1)
        {
            if (historicalDemand == null || historicalDemand.Count < 2)
                throw new ArgumentException("At least 2 periods of data required for trend smoothing.", nameof(historicalDemand));

            double level = historicalDemand[0];
            double trend = historicalDemand[1] - historicalDemand[0];

            for (int i = 1; i < historicalDemand.Count; i++)
            {
                double prevLevel = level;
                level = alpha * historicalDemand[i] + (1 - alpha) * (level + trend);
                trend = beta  * (level - prevLevel) + (1 - beta) * trend;
            }

            return Math.Round((decimal)(level + trend), 4);
        }

        // ── Seasonal Trend Analysis ───────────────────────────────────

        /// <summary>
        /// Calculate seasonal indices from monthly data (12-period cycle).
        /// Useful for festivals/wedding seasons in jewellery retail.
        /// </summary>
        /// <param name="monthlySales">Monthly sales quantities (at least 12 values).</param>
        /// <returns>Seasonal index per month (12 values; 1.0 = average).</returns>
        public List<double> CalculateSeasonalIndices(IList<int> monthlySales)
        {
            if (monthlySales == null || monthlySales.Count < 12)
                throw new ArgumentException("At least 12 months of data required.", nameof(monthlySales));

            var indices = new List<double>(12);
            double avg = monthlySales.Average();
            if (avg == 0) return Enumerable.Repeat(1.0, 12).ToList();

            // Use the first complete 12-month cycle
            for (int m = 0; m < 12; m++)
            {
                double monthlyAvg = 0;
                int count = 0;
                for (int i = m; i < monthlySales.Count; i += 12)
                {
                    monthlyAvg += monthlySales[i];
                    count++;
                }
                monthlyAvg /= count;
                indices.Add(monthlyAvg / avg);
            }

            return indices;
        }

        // ── Full Forecast Pipeline ────────────────────────────────────

        /// <summary>
        /// Run a full monthly demand forecast for an item, persisting results to the database.
        /// </summary>
        /// <param name="itemId">Item to forecast.</param>
        /// <param name="locationId">Optional location filter.</param>
        /// <param name="periodsBack">Number of historical months to use as training data.</param>
        /// <param name="alpha">Smoothing factor.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Forecasted demand for the next month.</returns>
        public async Task<decimal> ForecastNextMonthAsync(
            int itemId, int? locationId, int periodsBack = 12, double alpha = 0.3,
            CancellationToken ct = default)
        {
            var now   = DateTime.UtcNow;
            var from  = now.AddMonths(-periodsBack);
            var monthly = await _salesHistoryRepo.GetMonthlySalesAsync(
                itemId, locationId, from, now, ct);

            if (!monthly.Any())
            {
                _logger.LogWarning("No sales history for item {ItemId}. Returning 0 forecast.", itemId);
                return 0m;
            }

            var demand = monthly.Select(m => m.TotalQty).ToList();
            var forecasted = ExponentialSmoothing(demand, alpha);

            // Persist forecast record
            var record = new ForecastData
            {
                ItemId             = itemId,
                LocationId         = locationId,
                PeriodStart        = new DateTime(now.Year, now.Month, 1).AddMonths(1),
                PeriodEnd          = new DateTime(now.Year, now.Month, 1).AddMonths(2).AddDays(-1),
                ActualQuantity     = 0,       // filled in later when period closes
                ForecastedQuantity = forecasted,
                ForecastError      = 0m,
                Granularity        = "Month"
            };

            await _forecastRepo.UpsertAsync(record, ct);

            _logger.LogInformation(
                "Forecasted {Qty} units for item {ItemId} next month (alpha={Alpha}).",
                forecasted, itemId, alpha);

            return forecasted;
        }

        /// <summary>
        /// Update actual sales for a closed period and recalculate forecast error.
        /// </summary>
        public async Task RecordActualAndUpdateErrorAsync(
            int itemId, int? locationId, DateTime periodStart, int actualQty,
            CancellationToken ct = default)
        {
            var history = await _forecastRepo.GetHistoryAsync(itemId, locationId, 1, "Month", ct);
            var record  = history.FirstOrDefault(fd => fd.PeriodStart == periodStart);

            if (record == null)
            {
                _logger.LogWarning(
                    "No forecast record found for item {ItemId} period {Period}.", itemId, periodStart);
                return;
            }

            record.ActualQuantity = actualQty;
            if (record.ForecastedQuantity > 0)
            {
                var error = Math.Abs((decimal)actualQty - record.ForecastedQuantity)
                            / record.ForecastedQuantity * 100m;
                record.ForecastError = Math.Round(error, 4);
            }

            await _forecastRepo.UpsertAsync(record, ct);
        }

        /// <summary>Get the mean absolute percentage error (MAPE) for an item's forecasts.</summary>
        public async Task<decimal> GetForecastAccuracyAsync(int itemId, CancellationToken ct = default)
        {
            var mape = await _forecastRepo.GetMapeAsync(itemId, ct);
            var accuracy = 100m - mape;
            return Math.Max(0m, Math.Round(accuracy, 2));
        }

        /// <summary>Detect potential stock-out risk based on current stock and forecast.</summary>
        public async Task<bool> IsStockOutRiskAsync(
            int itemId, int locationId, int currentStock, int? leadTimeDays,
            double alpha = 0.3, CancellationToken ct = default)
        {
            var forecastedMonthly = await ForecastNextMonthAsync(itemId, locationId, alpha: alpha, ct: ct);
            var daysInMonth = 30m;
            var dailyDemand = forecastedMonthly / daysInMonth;
            var consumptionDuringLead = dailyDemand * (leadTimeDays ?? 7);

            return (decimal)currentStock <= consumptionDuringLead;
        }
    }
}
