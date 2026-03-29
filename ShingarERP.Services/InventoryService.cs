using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Constants;
using ShingarERP.Core.DTOs;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;

namespace ShingarERP.Services
{
    /// <summary>
    /// Business logic service for Metal and Finished Goods inventory (Modules 01 & 02).
    /// </summary>
    public class InventoryService
    {
        private readonly IUnitOfWork        _uow;
        private readonly InventoryRepository _inventoryRepo;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(
            IUnitOfWork uow,
            InventoryRepository inventoryRepo,
            ILogger<InventoryService> logger)
        {
            _uow           = uow;
            _inventoryRepo = inventoryRepo;
            _logger        = logger;
        }

        // ── Metal Management ─────────────────────────────────────────

        /// <summary>Get all active metals.</summary>
        public async Task<IEnumerable<MetalDto>> GetAllMetalsAsync(CancellationToken ct = default)
        {
            var metals = await _uow.Repository<Metal>().FindAsync(m => m.IsActive, ct);
            return metals.Select(MapMetal);
        }

        /// <summary>Create a new metal purchase lot.</summary>
        public async Task<MetalLotDto> CreateMetalLotAsync(CreateMetalLotRequest request, CancellationToken ct = default)
        {
            // Validate supplier exists
            var supplier = await _uow.Repository<Supplier>().GetByIdAsync(request.SupplierId, ct)
                ?? throw new InvalidOperationException($"Supplier {request.SupplierId} not found.");

            // Validate metal exists
            var metal = await _uow.Repository<Metal>().GetByIdAsync(request.MetalId, ct)
                ?? throw new InvalidOperationException($"Metal {request.MetalId} not found.");

            // Validate lot number uniqueness
            if (await _uow.Repository<MetalLot>().AnyAsync(l => l.LotNumber == request.LotNumber, ct))
                throw new InvalidOperationException($"Lot number '{request.LotNumber}' already exists.");

            // Compute melting loss if not supplied
            var meltingLoss = request.MeltingLossPercent > 0
                ? request.MeltingLossPercent
                : (request.GrossWeight - request.NetWeight) / request.GrossWeight * 100m;

            var lot = new MetalLot
            {
                LotNumber          = request.LotNumber,
                MetalId            = request.MetalId,
                SupplierId         = request.SupplierId,
                GrossWeight        = request.GrossWeight,
                NetWeight          = request.NetWeight,
                MeltingLossPercent = Math.Round(meltingLoss, 4),
                RemainingWeight    = request.NetWeight,
                PurchaseRatePerGram= request.PurchaseRatePerGram,
                TotalCost          = Math.Round(request.NetWeight * request.PurchaseRatePerGram, 4),
                PurchaseDate       = request.PurchaseDate,
                Remarks            = request.Remarks
            };

            await _uow.Repository<MetalLot>().AddAsync(lot, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Metal lot {LotNumber} created. Net weight: {Weight}g", lot.LotNumber, lot.NetWeight);

            return MapMetalLot(lot, metal.MetalType + " " + metal.PurityCode, supplier.SupplierName);
        }

        /// <summary>Consume metal from a lot (e.g., during manufacturing).</summary>
        public async Task ConsumeLotWeightAsync(int lotId, decimal weightToConsume, string reason, CancellationToken ct = default)
        {
            var lot = await _uow.Repository<MetalLot>().GetByIdAsync(lotId, ct)
                ?? throw new InvalidOperationException($"Lot {lotId} not found.");

            if (weightToConsume > lot.RemainingWeight)
                throw new InvalidOperationException(
                    $"Insufficient weight. Available: {lot.RemainingWeight}g, Requested: {weightToConsume}g.");

            lot.RemainingWeight -= weightToConsume;
            lot.UpdatedAt        = DateTime.UtcNow;

            _uow.Repository<MetalLot>().Update(lot);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Consumed {Weight}g from lot {LotId}. Remaining: {Remaining}g. Reason: {Reason}",
                weightToConsume, lotId, lot.RemainingWeight, reason);
        }

        /// <summary>Get stock summary per metal with minimum stock alert flag.</summary>
        public async Task<List<(MetalDto Metal, decimal TotalGrams, bool BelowMinimum)>> GetMetalStockSummaryAsync(CancellationToken ct = default)
        {
            var metals = await _uow.Repository<Metal>().FindAsync(m => m.IsActive, ct);
            var result = new List<(MetalDto, decimal, bool)>();

            foreach (var metal in metals)
            {
                var totalGrams = await _inventoryRepo.GetTotalRemainingWeightAsync(metal.MetalId, ct);
                var minStock   = GetMinStockThreshold(metal.PurityCode);
                result.Add((MapMetal(metal), totalGrams, totalGrams < minStock));
            }

            return result;
        }

        // ── Finished Goods Management ────────────────────────────────

        /// <summary>Create a new finished goods SKU.</summary>
        public async Task<FinishedGoodDto> CreateFinishedGoodAsync(CreateFinishedGoodRequest request, CancellationToken ct = default)
        {
            // Validate uniqueness
            if (await _uow.Repository<FinishedGood>().AnyAsync(f => f.SKU == request.SKU, ct))
                throw new InvalidOperationException($"SKU '{request.SKU}' already exists.");

            if (!string.IsNullOrWhiteSpace(request.BarcodeNumber) &&
                await _uow.Repository<FinishedGood>().AnyAsync(f => f.BarcodeNumber == request.BarcodeNumber, ct))
                throw new InvalidOperationException($"Barcode '{request.BarcodeNumber}' already exists.");

            var category = await _uow.Repository<ItemCategory>().GetByIdAsync(request.CategoryId, ct)
                ?? throw new InvalidOperationException($"Category {request.CategoryId} not found.");

            var metal = await _uow.Repository<Metal>().GetByIdAsync(request.MetalId, ct)
                ?? throw new InvalidOperationException($"Metal {request.MetalId} not found.");

            var item = new FinishedGood
            {
                SKU                 = request.SKU,
                ItemName            = request.ItemName,
                CategoryId          = request.CategoryId,
                MetalId             = request.MetalId,
                GrossWeight         = request.GrossWeight,
                NetWeight           = request.NetWeight,
                StoneWeight         = request.StoneWeight,
                MakingChargePerGram = request.MakingChargePerGram,
                MakingChargePercent = request.MakingChargePercent,
                BarcodeNumber       = request.BarcodeNumber,
                StockLocation       = request.StockLocation,
                SalePrice           = request.SalePrice,
                Description         = request.Description
            };

            await _uow.Repository<FinishedGood>().AddAsync(item, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Finished good SKU {SKU} created.", item.SKU);

            return MapFinishedGood(item, category.CategoryName, metal.MetalType + " " + metal.PurityCode);
        }

        /// <summary>Transfer stock between locations.</summary>
        public async Task TransferStockAsync(StockAdjustmentRequest request, CancellationToken ct = default)
        {
            var item = await _uow.Repository<FinishedGood>().GetByIdAsync(request.ItemId, ct)
                ?? throw new InvalidOperationException($"Item {request.ItemId} not found.");

            if (item.StockQuantity + request.QuantityChange < 0)
                throw new InvalidOperationException("Insufficient stock for transfer.");

            // Log transaction
            var tx = new StockTransaction
            {
                ItemId           = request.ItemId,
                VoucherNo        = $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}",
                TransactionType  = request.AdjustmentType,
                QuantityIn       = request.QuantityChange > 0 ? request.QuantityChange : 0,
                QuantityOut      = request.QuantityChange < 0 ? Math.Abs(request.QuantityChange) : 0,
                FromLocation     = request.FromLocation ?? item.StockLocation,
                ToLocation       = request.ToLocation,
                TransactionDate  = DateTime.UtcNow,
                Remarks          = request.Remarks
            };

            await _uow.Repository<StockTransaction>().AddAsync(tx, ct);

            // Update item location if specified
            if (!string.IsNullOrWhiteSpace(request.ToLocation))
                item.StockLocation = request.ToLocation;

            item.StockQuantity += request.QuantityChange;
            item.UpdatedAt      = DateTime.UtcNow;

            _uow.Repository<FinishedGood>().Update(item);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Stock adjusted for item {SKU}. Change: {Change}", item.SKU, request.QuantityChange);
        }

        /// <summary>Get paged finished goods list.</summary>
        public async Task<(List<FinishedGoodDto> Items, int TotalCount)> GetFinishedGoodsAsync(
            string? search, string? location, int? categoryId, int page = 1, int pageSize = 25,
            CancellationToken ct = default)
        {
            var (items, total) = await _inventoryRepo.GetFinishedGoodsPagedAsync(
                search, location, categoryId, page, pageSize, ct);

            return (items.Select(f => MapFinishedGood(f, f.Category.CategoryName,
                f.Metal.MetalType + " " + f.Metal.PurityCode)).ToList(), total);
        }

        /// <summary>Get aging / dead stock items.</summary>
        public async Task<List<FinishedGoodDto>> GetAgingStockAsync(int daysThreshold = 180, CancellationToken ct = default)
        {
            var items = await _inventoryRepo.GetAgingStockAsync(daysThreshold, ct);
            return items.Select(f => MapFinishedGood(f, f.Category?.CategoryName ?? string.Empty,
                f.Metal?.MetalType + " " + f.Metal?.PurityCode)).ToList();
        }

        // ── Stone Management ─────────────────────────────────────────

        /// <summary>Register a new stone/diamond in inventory.</summary>
        public async Task<StoneDto> RegisterStoneAsync(CreateStoneRequest request, CancellationToken ct = default)
        {
            if (await _uow.Repository<Stone>().AnyAsync(s => s.StoneCode == request.StoneCode, ct))
                throw new InvalidOperationException($"Stone code '{request.StoneCode}' already exists.");

            if (!string.IsNullOrWhiteSpace(request.CertificateNo) &&
                await _uow.Repository<Stone>().AnyAsync(s => s.CertificateNo == request.CertificateNo, ct))
                throw new InvalidOperationException($"Certificate number '{request.CertificateNo}' already exists.");

            var stone = new Stone
            {
                StoneCode     = request.StoneCode,
                StoneType     = request.StoneType,
                CertificateNo = request.CertificateNo,
                CertLab       = request.CertLab,
                CaratWeight   = request.CaratWeight,
                Color         = request.Color,
                Clarity       = request.Clarity,
                Cut           = request.Cut,
                Shape         = request.Shape,
                PurchasePrice = request.PurchasePrice,
                SalePrice     = request.SalePrice,
                IsConsignment = request.IsConsignment,
                SupplierId    = request.SupplierId,
                Status        = "Available"
            };

            await _uow.Repository<Stone>().AddAsync(stone, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Stone {StoneCode} registered. Type: {Type}, Carat: {Carat}",
                stone.StoneCode, stone.StoneType, stone.CaratWeight);

            return MapStone(stone);
        }

        // ── Mapping helpers ──────────────────────────────────────────

        private static MetalDto MapMetal(Metal m) => new()
        {
            MetalId    = m.MetalId,
            MetalType  = m.MetalType,
            PurityCode = m.PurityCode,
            Fineness   = m.Fineness,
            WeightUnit = m.WeightUnit,
            IsActive   = m.IsActive
        };

        private static MetalLotDto MapMetalLot(MetalLot lot, string metalName, string supplierName) => new()
        {
            LotId               = lot.LotId,
            LotNumber           = lot.LotNumber,
            MetalId             = lot.MetalId,
            MetalName           = metalName,
            SupplierId          = lot.SupplierId,
            SupplierName        = supplierName,
            GrossWeight         = lot.GrossWeight,
            NetWeight           = lot.NetWeight,
            MeltingLossPercent  = lot.MeltingLossPercent,
            RemainingWeight     = lot.RemainingWeight,
            PurchaseRatePerGram = lot.PurchaseRatePerGram,
            TotalCost           = lot.TotalCost,
            PurchaseDate        = lot.PurchaseDate,
            Remarks             = lot.Remarks
        };

        private static FinishedGoodDto MapFinishedGood(FinishedGood f, string categoryName, string metalName) => new()
        {
            ItemId               = f.ItemId,
            SKU                  = f.SKU,
            ItemName             = f.ItemName,
            CategoryId           = f.CategoryId,
            CategoryName         = categoryName,
            MetalId              = f.MetalId,
            MetalName            = metalName,
            GrossWeight          = f.GrossWeight,
            NetWeight            = f.NetWeight,
            StoneWeight          = f.StoneWeight,
            MakingChargePerGram  = f.MakingChargePerGram,
            MakingChargePercent  = f.MakingChargePercent,
            BarcodeNumber        = f.BarcodeNumber,
            PhotoPath            = f.PhotoPath,
            StockLocation        = f.StockLocation,
            StockQuantity        = f.StockQuantity,
            SalePrice            = f.SalePrice,
            Description          = f.Description,
            IsActive             = f.IsActive
        };

        private static StoneDto MapStone(Stone s) => new()
        {
            StoneId       = s.StoneId,
            StoneCode     = s.StoneCode,
            StoneType     = s.StoneType,
            CertificateNo = s.CertificateNo,
            CertLab       = s.CertLab,
            CaratWeight   = s.CaratWeight,
            Color         = s.Color,
            Clarity       = s.Clarity,
            Cut           = s.Cut,
            Shape         = s.Shape,
            PurchasePrice = s.PurchasePrice,
            SalePrice     = s.SalePrice,
            IsConsignment = s.IsConsignment,
            Status        = s.Status
        };

        private static decimal GetMinStockThreshold(string purityCode) => purityCode switch
        {
            "24K"  => AppConstants.MinStockAlert.Gold24K,
            "22K"  => AppConstants.MinStockAlert.Gold22K,
            "99.9" => AppConstants.MinStockAlert.Silver99,
            _      => 50m
        };
    }
}
