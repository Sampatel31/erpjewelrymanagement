using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;
using ShingarERP.Data.Repositories;

namespace ShingarERP.Services
{
    /// <summary>
    /// Barcode management service: generation, validation, inventory lookup,
    /// and integrity checks.
    /// </summary>
    public class BarcodeManagementService
    {
        private readonly IUnitOfWork       _uow;
        private readonly BarcodeRepository _barcodeRepo;
        private readonly ILogger<BarcodeManagementService> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="BarcodeManagementService"/>.
        /// </summary>
        public BarcodeManagementService(
            IUnitOfWork uow,
            BarcodeRepository barcodeRepo,
            ILogger<BarcodeManagementService> logger)
        {
            _uow         = uow;
            _barcodeRepo = barcodeRepo;
            _logger      = logger;
        }

        // ── Barcode Generation ────────────────────────────────────────

        /// <summary>
        /// Generate a unique EAN-13 compatible barcode value for a finished good.
        /// Format: prefix (3 digits) + itemId (6 digits) + locationId (3 digits) + check digit = 13 digits total.
        /// </summary>
        public string GenerateEan13(int itemId, int locationId, string prefix = "890")
        {
            if (prefix.Length != 3 || !prefix.All(char.IsDigit))
                throw new ArgumentException("EAN-13 prefix must be exactly 3 digits.", nameof(prefix));

            // 3 + 6 + 3 = 12-digit body; check digit makes 13 total
            var body = $"{prefix}{itemId:D6}{locationId:D3}";
            if (body.Length != 12)
                body = body.PadRight(12, '0').Substring(0, 12);

            var checkDigit = CalculateEan13CheckDigit(body);
            return $"{body}{checkDigit}";
        }

        /// <summary>
        /// Calculate the EAN-13 check digit for a 12-digit barcode body.
        /// Uses the standard alternating 1/3 weight scheme.
        /// </summary>
        public int CalculateEan13CheckDigit(string twelveDigits)
        {
            if (twelveDigits.Length != 12 || !twelveDigits.All(char.IsDigit))
                throw new ArgumentException("Input must be exactly 12 numeric digits.", nameof(twelveDigits));

            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int digit  = twelveDigits[i] - '0';
                int weight = (i % 2 == 0) ? 1 : 3;
                sum += digit * weight;
            }

            return (10 - (sum % 10)) % 10;
        }

        /// <summary>Validate that a 13-digit EAN-13 barcode has the correct check digit.</summary>
        public bool ValidateEan13(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode) || barcode.Length != 13 || !barcode.All(char.IsDigit))
                return false;

            var expected = CalculateEan13CheckDigit(barcode.Substring(0, 12));
            var actual   = barcode[12] - '0';
            return expected == actual;
        }

        /// <summary>
        /// Generate a unique QR-code value containing item metadata.
        /// Format: JSON-like compact string, SHA-256 truncated for uniqueness.
        /// </summary>
        public string GenerateQrCode(int itemId, string sku, int locationId)
        {
            var raw  = $"SHNGR|{itemId}|{sku}|{locationId}|{DateTime.UtcNow.Ticks}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            // Use the first 8 bytes (16 hex chars) for a compact yet unique identifier
            var hex  = string.Concat(hash.Take(8).Select(b => b.ToString("X2")));
            return $"QR-{itemId:D6}-{hex}";
        }

        // ── Barcode Registration ──────────────────────────────────────

        /// <summary>
        /// Register a new barcode for a finished good, generating one if not provided.
        /// </summary>
        public async Task<BarcodeInfo> RegisterBarcodeAsync(
            int itemId, int? locationId, string barcodeType = "EAN13",
            string? explicitValue = null, string? printedBy = null,
            CancellationToken ct = default)
        {
            var item = await _uow.Repository<FinishedGood>().GetByIdAsync(itemId, ct)
                ?? throw new InvalidOperationException($"Item {itemId} not found.");

            string barcodeValue;
            if (!string.IsNullOrWhiteSpace(explicitValue))
            {
                barcodeValue = explicitValue;
            }
            else if (barcodeType == "EAN13")
            {
                barcodeValue = GenerateEan13(itemId, locationId ?? 0);
            }
            else
            {
                barcodeValue = GenerateQrCode(itemId, item.SKU, locationId ?? 0);
                barcodeType  = "QRCode";
            }

            // Ensure uniqueness
            if (await _barcodeRepo.ExistsAsync(barcodeValue, ct))
                throw new InvalidOperationException($"Barcode '{barcodeValue}' already exists.");

            var info = new BarcodeInfo
            {
                BarcodeValue = barcodeValue,
                BarcodeType  = barcodeType,
                ItemId       = itemId,
                LocationId   = locationId,
                PrintedBy    = printedBy,
                PrintedAt    = DateTime.UtcNow
            };

            await _uow.Repository<BarcodeInfo>().AddAsync(info, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Barcode {Value} ({Type}) registered for item {ItemId}.", barcodeValue, barcodeType, itemId);

            return info;
        }

        // ── Lookup & Integrity ────────────────────────────────────────

        /// <summary>Look up inventory item by scanning a barcode value.</summary>
        public async Task<BarcodeInfo?> LookupByBarcodeAsync(string barcodeValue, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(barcodeValue))
                throw new ArgumentException("Barcode value cannot be empty.", nameof(barcodeValue));

            return await _barcodeRepo.GetByValueAsync(barcodeValue, ct);
        }

        /// <summary>Get all active barcodes for an item.</summary>
        public async Task<List<BarcodeInfo>> GetItemBarcodesAsync(int itemId, CancellationToken ct = default)
            => await _barcodeRepo.GetByItemAsync(itemId, ct);

        /// <summary>Get barcodes printed for a specific location.</summary>
        public async Task<List<BarcodeInfo>> GetLocationBarcodesAsync(int locationId, CancellationToken ct = default)
            => await _barcodeRepo.GetByLocationAsync(locationId, ct);

        /// <summary>
        /// Run an integrity check: verifies every EAN-13 barcode stored for an item
        /// has a valid check digit.
        /// </summary>
        public async Task<List<string>> RunIntegrityCheckAsync(int itemId, CancellationToken ct = default)
        {
            var barcodes = await _barcodeRepo.GetByItemAsync(itemId, ct);
            var invalid  = new List<string>();

            foreach (var bc in barcodes)
            {
                if (bc.BarcodeType == "EAN13" && !ValidateEan13(bc.BarcodeValue))
                    invalid.Add(bc.BarcodeValue);
            }

            if (invalid.Any())
                _logger.LogWarning("{Count} invalid EAN-13 barcodes found for item {ItemId}.", invalid.Count, itemId);

            return invalid;
        }

        /// <summary>Deactivate all barcodes for an item (e.g., before re-labelling).</summary>
        public async Task DeactivateBarcodesAsync(int itemId, CancellationToken ct = default)
        {
            await _barcodeRepo.DeactivateByItemAsync(itemId, ct);
            _logger.LogInformation("All barcodes deactivated for item {ItemId}.", itemId);
        }
    }
}
