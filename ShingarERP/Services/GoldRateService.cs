using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.DTOs;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;

namespace ShingarERP.Services
{
    /// <summary>
    /// Service for fetching and storing live MCX gold/silver rates.
    /// Falls back to manual rate entry when API is unavailable.
    /// </summary>
    public class GoldRateService
    {
        private readonly IUnitOfWork _uow;
        private readonly HttpClient  _httpClient;
        private readonly ILogger<GoldRateService> _logger;

        // Cache last fetched rate to avoid hammering the API
        private MetalRate? _lastCachedRate;
        private DateTime   _lastCacheTime = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public GoldRateService(IUnitOfWork uow, HttpClient httpClient, ILogger<GoldRateService> logger)
        {
            _uow        = uow;
            _httpClient = httpClient;
            _logger     = logger;
        }

        /// <summary>
        /// Get the current gold rate per gram for a given metal.
        /// Tries MCX API first; on failure returns last saved rate.
        /// </summary>
        public async Task<MetalRateDto?> GetCurrentRateAsync(int metalId, CancellationToken ct = default)
        {
            // Return cached value if fresh
            if (_lastCachedRate != null &&
                _lastCachedRate.MetalId == metalId &&
                DateTime.UtcNow - _lastCacheTime < _cacheDuration)
            {
                return MapToDto(_lastCachedRate);
            }

            // Try to fetch live rate
            try
            {
                var liveRate = await FetchLiveRateAsync(metalId, ct);
                if (liveRate != null)
                {
                    await SaveRateAsync(liveRate, ct);
                    _lastCachedRate = liveRate;
                    _lastCacheTime  = DateTime.UtcNow;
                    return MapToDto(liveRate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch live gold rate from API. Falling back to database.");
            }

            // Fallback to last DB rate
            var repo   = _uow.Repository<MetalRate>();
            var latest = await repo.FirstOrDefaultAsync(r => r.MetalId == metalId, ct);
            return latest != null ? MapToDto(latest) : null;
        }

        /// <summary>Save a manually entered metal rate.</summary>
        public async Task<MetalRateDto> SaveManualRateAsync(CreateMetalRateRequest request, CancellationToken ct = default)
        {
            var metal = await _uow.Repository<Metal>().GetByIdAsync(request.MetalId, ct)
                ?? throw new InvalidOperationException($"Metal {request.MetalId} not found.");

            // 1 tola = 11.6638 grams
            const decimal tolaFactor = 11.6638m;

            var rate = new MetalRate
            {
                MetalId      = request.MetalId,
                RatePerGram  = request.RatePerGram,
                RatePerTola  = Math.Round(request.RatePerGram * tolaFactor, 4),
                MCXSpotRate  = request.MCXSpotRate,
                RateDate     = request.RateDate.Date,
                Source       = request.Source
            };

            await _uow.Repository<MetalRate>().AddAsync(rate, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Rate saved for metal {MetalId}: ₹{Rate}/g on {Date}",
                request.MetalId, request.RatePerGram, request.RateDate.Date.ToShortDateString());

            return MapToDto(rate);
        }

        /// <summary>
        /// Calculate jewellery sale price based on weight and current gold rate.
        /// Formula: (NetWeight × RatePerGram) + (MakingChargePerGram × NetWeight) + StonePrice
        /// </summary>
        public async Task<decimal> CalculateSalePriceAsync(
            int metalId, decimal netWeightGrams, decimal makingChargePerGram,
            decimal stonePrice = 0, CancellationToken ct = default)
        {
            var rateDto = await GetCurrentRateAsync(metalId, ct);
            if (rateDto == null)
                throw new InvalidOperationException("No gold rate available. Please enter today's rate.");

            var metalValue   = netWeightGrams * rateDto.RatePerGram;
            var makingCharge = netWeightGrams * makingChargePerGram;
            var total        = metalValue + makingCharge + stonePrice;

            _logger.LogDebug("Price calc: Metal={Metal:F2} + Making={Making:F2} + Stone={Stone:F2} = {Total:F2}",
                metalValue, makingCharge, stonePrice, total);

            return Math.Round(total, 2);
        }

        // ── Private helpers ──────────────────────────────────────────

        /// <summary>
        /// Attempt to fetch live rate from MCX-like public API.
        /// NOTE: Replace the URL with a real MCX feed / your data provider.
        /// </summary>
        private async Task<MetalRate?> FetchLiveRateAsync(int metalId, CancellationToken ct)
        {
            // Placeholder – real implementation would call an MCX API endpoint.
            // Example endpoint structure (replace with real subscription-based API):
            // GET https://api.mcxindia.com/v1/rates?symbol=GOLD&apikey={key}
            //
            // For now we return null so the service gracefully falls back.
            await Task.CompletedTask;
            return null;
        }

        private async Task SaveRateAsync(MetalRate rate, CancellationToken ct)
        {
            await _uow.Repository<MetalRate>().AddAsync(rate, ct);
            await _uow.SaveChangesAsync(ct);
        }

        private static MetalRateDto MapToDto(MetalRate r) => new()
        {
            RateId      = r.RateId,
            MetalId     = r.MetalId,
            RatePerGram = r.RatePerGram,
            RatePerTola = r.RatePerTola,
            MCXSpotRate = r.MCXSpotRate,
            RateDate    = r.RateDate,
            Source      = r.Source
        };
    }
}
