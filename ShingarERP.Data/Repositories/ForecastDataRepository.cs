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
    /// Repository for forecast data aggregation and retrieval.
    /// </summary>
    public class ForecastDataRepository
    {
        private readonly ShingarContext _context;

        /// <summary>Initialises a new instance of <see cref="ForecastDataRepository"/>.</summary>
        public ForecastDataRepository(ShingarContext context) => _context = context;

        /// <summary>Get historical forecast data for an item, optionally filtered by location.</summary>
        public async Task<List<ForecastData>> GetHistoryAsync(
            int itemId, int? locationId, int periodsBack, string granularity = "Month",
            CancellationToken ct = default)
        {
            var cutoff = granularity == "Week"
                ? DateTime.UtcNow.AddDays(-periodsBack * 7)
                : DateTime.UtcNow.AddMonths(-periodsBack);

            var query = _context.ForecastData
                .AsNoTracking()
                .Where(fd => fd.ItemId == itemId
                          && fd.Granularity == granularity
                          && fd.PeriodStart >= cutoff);

            if (locationId.HasValue)
                query = query.Where(fd => fd.LocationId == locationId.Value);

            return await query.OrderBy(fd => fd.PeriodStart).ToListAsync(ct);
        }

        /// <summary>Get aggregate actual sales quantities grouped by period.</summary>
        public async Task<List<(DateTime PeriodStart, int TotalQuantity)>> GetAggregatedSalesAsync(
            int itemId, int? locationId, int periodsBack, string granularity = "Month",
            CancellationToken ct = default)
        {
            var data = await GetHistoryAsync(itemId, locationId, periodsBack, granularity, ct);
            return data
                .GroupBy(fd => fd.PeriodStart)
                .Select(g => (g.Key, g.Sum(fd => fd.ActualQuantity)))
                .OrderBy(t => t.Key)
                .ToList();
        }

        /// <summary>Upsert a forecast record for a given period.</summary>
        public async Task UpsertAsync(ForecastData record, CancellationToken ct = default)
        {
            var existing = await _context.ForecastData
                .FirstOrDefaultAsync(fd =>
                    fd.ItemId == record.ItemId &&
                    fd.PeriodStart == record.PeriodStart &&
                    fd.Granularity == record.Granularity &&
                    fd.LocationId == record.LocationId, ct);

            if (existing == null)
                _context.ForecastData.Add(record);
            else
            {
                existing.ActualQuantity    = record.ActualQuantity;
                existing.ForecastedQuantity = record.ForecastedQuantity;
                existing.ForecastError     = record.ForecastError;
            }

            await _context.SaveChangesAsync(ct);
        }

        /// <summary>Calculate mean absolute percentage error for an item's forecast history.</summary>
        public async Task<decimal> GetMapeAsync(int itemId, CancellationToken ct = default)
        {
            var records = await _context.ForecastData
                .AsNoTracking()
                .Where(fd => fd.ItemId == itemId && fd.ActualQuantity > 0)
                .ToListAsync(ct);

            if (!records.Any()) return 0m;
            return records.Average(fd => fd.ForecastError);
        }
    }
}
