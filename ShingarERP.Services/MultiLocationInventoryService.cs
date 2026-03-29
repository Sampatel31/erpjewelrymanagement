using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;

namespace ShingarERP.Services
{
    /// <summary>
    /// Manages inventory across multiple physical locations with transfer support,
    /// capacity constraints, and warehouse balancing.
    /// </summary>
    public class MultiLocationInventoryService
    {
        private readonly IUnitOfWork                    _uow;
        private readonly LocationInventoryRepository    _locationInventoryRepo;
        private readonly StockTransferRepository        _stockTransferRepo;
        private readonly ILogger<MultiLocationInventoryService> _logger;

        /// <summary>
        /// Initialises a new instance of <see cref="MultiLocationInventoryService"/>.
        /// </summary>
        public MultiLocationInventoryService(
            IUnitOfWork uow,
            LocationInventoryRepository locationInventoryRepo,
            StockTransferRepository stockTransferRepo,
            ILogger<MultiLocationInventoryService> logger)
        {
            _uow                   = uow;
            _locationInventoryRepo = locationInventoryRepo;
            _stockTransferRepo     = stockTransferRepo;
            _logger                = logger;
        }

        // ── Location Management ───────────────────────────────────────

        /// <summary>Get all active inventory locations.</summary>
        public async Task<IEnumerable<InventoryLocation>> GetActiveLocationsAsync(CancellationToken ct = default)
            => await _uow.Repository<InventoryLocation>().FindAsync(l => l.IsActive, ct);

        /// <summary>Create a new inventory location.</summary>
        public async Task<InventoryLocation> CreateLocationAsync(
            string code, string name, string type, string? address = null, CancellationToken ct = default)
        {
            if (await _uow.Repository<InventoryLocation>().AnyAsync(l => l.LocationCode == code, ct))
                throw new InvalidOperationException($"Location code '{code}' already exists.");

            var location = new InventoryLocation
            {
                LocationCode = code,
                LocationName = name,
                LocationType = type,
                Address      = address
            };

            await _uow.Repository<InventoryLocation>().AddAsync(location, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Location {Code} ({Name}) created.", code, name);
            return location;
        }

        // ── Stock Level Queries ───────────────────────────────────────

        /// <summary>Get current stock level for an item at a specific location.</summary>
        public async Task<int> GetStockLevelAsync(int locationId, int itemId, CancellationToken ct = default)
        {
            var li = await _locationInventoryRepo.GetOrCreateAsync(locationId, itemId, ct);
            return li.Quantity;
        }

        /// <summary>Get stock distribution for an item across all locations.</summary>
        public async Task<List<LocationInventory>> GetStockDistributionAsync(int itemId, CancellationToken ct = default)
            => await _locationInventoryRepo.GetByItemAsync(itemId, ct);

        /// <summary>Get total stock for an item across all locations.</summary>
        public async Task<int> GetTotalStockAsync(int itemId, CancellationToken ct = default)
            => await _locationInventoryRepo.GetTotalQuantityAsync(itemId, ct);

        /// <summary>Get a summary of all locations (item count and total quantity).</summary>
        public async Task<List<(InventoryLocation Location, int TotalItems, int TotalQuantity)>>
            GetLocationSummaryAsync(CancellationToken ct = default)
            => await _locationInventoryRepo.GetLocationSummaryAsync(ct);

        // ── Stock Adjustment ─────────────────────────────────────────

        /// <summary>Adjust stock at a location (positive = add, negative = remove).</summary>
        public async Task AdjustStockAsync(
            int locationId, int itemId, int quantityChange, string reason, CancellationToken ct = default)
        {
            var li = await _locationInventoryRepo.GetOrCreateAsync(locationId, itemId, ct);

            if (li.Quantity + quantityChange < 0)
                throw new InvalidOperationException(
                    $"Insufficient stock at location {locationId}. Available: {li.Quantity}, Requested change: {quantityChange}.");

            li.Quantity    += quantityChange;
            li.LastUpdated  = DateTime.UtcNow;

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Stock adjusted: item {ItemId}, location {LocationId}, change {Change}, reason {Reason}.",
                itemId, locationId, quantityChange, reason);
        }

        // ── Stock Transfer ────────────────────────────────────────────

        /// <summary>
        /// Initiate a stock transfer between two locations.
        /// Validates capacity constraints before committing.
        /// </summary>
        public async Task<StockTransfer> InitiateTransferAsync(
            int fromLocationId, int toLocationId, int itemId, int quantity,
            string? remarks = null, string? initiatedBy = null, CancellationToken ct = default)
        {
            if (fromLocationId == toLocationId)
                throw new InvalidOperationException("Source and destination locations must be different.");

            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Transfer quantity must be positive.");

            // Validate source stock
            var sourceLi = await _locationInventoryRepo.GetOrCreateAsync(fromLocationId, itemId, ct);
            if (sourceLi.Quantity < quantity)
                throw new InvalidOperationException(
                    $"Insufficient stock. Available: {sourceLi.Quantity}, Requested: {quantity}.");

            // Validate capacity at destination
            await ValidateCapacityAsync(toLocationId, itemId, quantity, ct);

            var transferNo = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}-{fromLocationId}-{toLocationId}-{Guid.NewGuid():N}".Substring(0, 30);

            var transfer = new StockTransfer
            {
                TransferNo      = transferNo,
                FromLocationId  = fromLocationId,
                ToLocationId    = toLocationId,
                ItemId          = itemId,
                Quantity        = quantity,
                Status          = "Pending",
                Remarks         = remarks,
                TransferDate    = DateTime.UtcNow,
                InitiatedBy     = initiatedBy
            };

            await _uow.Repository<StockTransfer>().AddAsync(transfer, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Transfer {TransferNo} initiated: {Qty} of item {ItemId} from loc {From} to loc {To}.",
                transferNo, quantity, itemId, fromLocationId, toLocationId);

            return transfer;
        }

        /// <summary>
        /// Complete a pending transfer, deducting from source and adding to destination.
        /// </summary>
        public async Task CompleteTransferAsync(
            int transferId, string? approvedBy = null, CancellationToken ct = default)
        {
            var transfer = await _uow.Repository<StockTransfer>().GetByIdAsync(transferId, ct)
                ?? throw new InvalidOperationException($"Transfer {transferId} not found.");

            if (transfer.Status != "Pending")
                throw new InvalidOperationException($"Transfer is in '{transfer.Status}' status and cannot be completed.");

            await _uow.BeginTransactionAsync(ct);
            try
            {
                // Deduct from source
                var sourceLi = await _locationInventoryRepo.GetOrCreateAsync(transfer.FromLocationId, transfer.ItemId, ct);
                if (sourceLi.Quantity < transfer.Quantity)
                    throw new InvalidOperationException(
                        $"Insufficient stock at source. Available: {sourceLi.Quantity}, Required: {transfer.Quantity}.");

                sourceLi.Quantity   -= transfer.Quantity;
                sourceLi.LastUpdated = DateTime.UtcNow;

                // Add to destination
                var destLi = await _locationInventoryRepo.GetOrCreateAsync(transfer.ToLocationId, transfer.ItemId, ct);
                destLi.Quantity    += transfer.Quantity;
                destLi.LastUpdated  = DateTime.UtcNow;

                // Mark transfer complete
                transfer.Status        = "Completed";
                transfer.CompletedDate = DateTime.UtcNow;
                transfer.ApprovedBy    = approvedBy;
                transfer.UpdatedAt     = DateTime.UtcNow;

                _uow.Repository<StockTransfer>().Update(transfer);
                await _uow.SaveChangesAsync(ct);
                await _uow.CommitTransactionAsync(ct);

                _logger.LogInformation("Transfer {TransferId} completed by {ApprovedBy}.", transferId, approvedBy);
            }
            catch
            {
                await _uow.RollbackTransactionAsync(ct);
                throw;
            }
        }

        /// <summary>Cancel a pending transfer without moving any stock.</summary>
        public async Task CancelTransferAsync(int transferId, string reason, CancellationToken ct = default)
        {
            var transfer = await _uow.Repository<StockTransfer>().GetByIdAsync(transferId, ct)
                ?? throw new InvalidOperationException($"Transfer {transferId} not found.");

            if (transfer.Status == "Completed")
                throw new InvalidOperationException("Completed transfers cannot be cancelled.");

            transfer.Status    = "Cancelled";
            transfer.Remarks   = string.IsNullOrWhiteSpace(transfer.Remarks)
                                     ? $"Cancelled: {reason}"
                                     : $"{transfer.Remarks} | Cancelled: {reason}";
            transfer.UpdatedAt = DateTime.UtcNow;

            _uow.Repository<StockTransfer>().Update(transfer);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Transfer {TransferId} cancelled. Reason: {Reason}.", transferId, reason);
        }

        // ── Warehouse Balancing ───────────────────────────────────────

        /// <summary>
        /// Identify imbalanced stock across locations for an item and suggest
        /// transfers to achieve target distribution percentages.
        /// </summary>
        public async Task<List<(int FromLocationId, int ToLocationId, int SuggestedQty)>>
            GetBalancingSuggestionsAsync(int itemId, CancellationToken ct = default)
        {
            var distribution = await _locationInventoryRepo.GetByItemAsync(itemId, ct);
            if (distribution.Count < 2) return new List<(int, int, int)>();

            var totalQty    = distribution.Sum(li => li.Quantity);
            if (totalQty == 0) return new List<(int, int, int)>();

            var targetPerLoc = totalQty / distribution.Count;
            var suggestions  = new List<(int, int, int)>();

            var overStocked  = distribution.Where(li => li.Quantity > targetPerLoc).ToList();
            var underStocked = distribution.Where(li => li.Quantity < targetPerLoc).ToList();

            foreach (var under in underStocked)
            {
                var needed = targetPerLoc - under.Quantity;
                var over   = overStocked.FirstOrDefault(li => li.Quantity - targetPerLoc >= needed);
                if (over != null)
                {
                    suggestions.Add((over.LocationId, under.LocationId, needed));
                    over.Quantity -= needed; // update in-memory for multi-suggestion scenarios
                }
            }

            return suggestions;
        }

        // ── Capacity Validation ───────────────────────────────────────

        private async Task ValidateCapacityAsync(int locationId, int itemId, int additionalQty, CancellationToken ct)
        {
            // Get category for the item
            var item = await _uow.Repository<FinishedGood>().GetByIdAsync(itemId, ct);
            if (item == null) return;

            var capacity = await _uow.Repository<LocationCapacity>().FirstOrDefaultAsync(
                lc => lc.LocationId == locationId && (lc.CategoryId == item.CategoryId || lc.CategoryId == null), ct);
            if (capacity == null) return;

            var currentQty = await _locationInventoryRepo.GetTotalQuantityAsync(itemId, ct);
            // Calculate total at that location
            var locInv = await _locationInventoryRepo.GetByLocationAsync(locationId, ct);
            var locTotal = locInv.Sum(li => li.Quantity);

            if (locTotal + additionalQty > capacity.MaxCapacity)
                throw new InvalidOperationException(
                    $"Location {locationId} capacity ({capacity.MaxCapacity}) would be exceeded. " +
                    $"Current: {locTotal}, Requested: {additionalQty}.");
        }
    }
}
