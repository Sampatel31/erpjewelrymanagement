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
    /// Manages design creation, versioning, collection management, approval workflow,
    /// cost estimation, and trending/popularity metrics.
    /// </summary>
    public class DesignManagementService
    {
        private readonly IUnitOfWork _uow;
        private readonly DesignRepository _designRepo;
        private readonly ILogger<DesignManagementService> _logger;

        private static readonly HashSet<(string From, string To)> ValidTransitions = new()
        {
            ("Draft",    "Review"),
            ("Review",   "Approved"),
            ("Review",   "Draft"),
            ("Approved", "Active"),
            ("Active",   "Archived"),
            ("Draft",    "Archived"),
        };

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public DesignManagementService(IUnitOfWork uow, DesignRepository designRepo, ILogger<DesignManagementService> logger)
        {
            _uow = uow;
            _designRepo = designRepo;
            _logger = logger;
        }

        /// <summary>Creates a new design with Bill of Materials.</summary>
        public async Task<Design> CreateDesignAsync(
            string designCode, string designName, int? categoryId, string? metalType,
            string complexity, decimal estimatedLaborHours, decimal estimatedMetalWeight, decimal basePrice,
            int? collectionId, string? description,
            IEnumerable<(string MaterialType, string? Description, decimal Weight, string Unit, decimal CostPerUnit)>? bomItems = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(designCode))
                throw new InvalidOperationException("Design code is required.");

            var design = new Design
            {
                DesignCode           = designCode,
                DesignName           = designName,
                ItemCategoryId       = categoryId,
                MetalType            = metalType,
                Complexity           = complexity,
                EstimatedLaborHours  = estimatedLaborHours,
                EstimatedMetalWeight = estimatedMetalWeight,
                BasePrice            = basePrice,
                Status               = "Draft",
                CollectionId         = collectionId,
                Description          = description,
                PopularityScore      = 0,
                IsActive             = true,
                CreatedAt            = DateTime.UtcNow,
                UpdatedAt            = DateTime.UtcNow
            };

            if (bomItems != null)
            {
                foreach (var (mt, desc, weight, unit, cost) in bomItems)
                    design.BOMs.Add(new DesignBOM { MaterialType = mt, MaterialDescription = desc, EstimatedWeight = weight, Unit = unit, EstimatedCostPerUnit = cost });
            }

            design.History.Add(new DesignHistory
            {
                Version           = 1,
                ChangeSummary     = "Design created",
                ChangedByUserId   = 1,
                ChangedAt         = DateTime.UtcNow
            });

            await _uow.Repository<Design>().AddAsync(design, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Design '{Code}' created (Id={Id})", designCode, design.Id);
            return design;
        }

        /// <summary>Advances a design through the approval workflow state machine.</summary>
        public async Task<Design> AdvanceStatusAsync(int designId, string toStatus, int userId, string? changeSummary = null, CancellationToken ct = default)
        {
            var design = await _designRepo.GetWithDetailsAsync(designId, ct)
                ?? throw new InvalidOperationException($"Design {designId} not found.");

            if (!ValidTransitions.Contains((design.Status, toStatus)))
                throw new InvalidOperationException($"Invalid transition from '{design.Status}' to '{toStatus}'.");

            var nextVersion = (design.History.Any() ? design.History.Max(h => h.Version) : 0) + 1;
            design.History.Add(new DesignHistory
            {
                DesignId        = designId,
                Version         = nextVersion,
                ChangeSummary   = changeSummary ?? $"Status changed to {toStatus}",
                ChangedByUserId = userId,
                ChangedAt       = DateTime.UtcNow
            });

            design.Status    = toStatus;
            design.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<Design>().Update(design);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Design {Code} status → '{Status}'", design.DesignCode, toStatus);
            return design;
        }

        /// <summary>Estimates design cost based on BOM materials and labor hours.</summary>
        public async Task<decimal> EstimateCostAsync(int designId, decimal metalRatePerGram, decimal laborRatePerHour, decimal markupPercent = 20m, CancellationToken ct = default)
        {
            var design = await _designRepo.GetWithDetailsAsync(designId, ct)
                ?? throw new InvalidOperationException($"Design {designId} not found.");

            var materialCost = design.BOMs.Sum(b => b.EstimatedWeight * b.EstimatedCostPerUnit);
            var metalCost    = design.EstimatedMetalWeight * metalRatePerGram;
            var laborCost    = design.EstimatedLaborHours * laborRatePerHour;
            var baseCost     = materialCost + metalCost + laborCost;

            return Math.Round(baseCost * (1 + markupPercent / 100m), 2);
        }

        /// <summary>Increments the popularity score for a design (called when sold/ordered).</summary>
        public async Task IncrementPopularityAsync(int designId, CancellationToken ct = default)
        {
            var design = await _designRepo.GetByIdAsync(designId, ct)
                ?? throw new InvalidOperationException($"Design {designId} not found.");
            design.PopularityScore++;
            design.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<Design>().Update(design);
            await _uow.SaveChangesAsync(ct);
        }

        /// <summary>Returns trending designs ordered by popularity.</summary>
        public async Task<IEnumerable<Design>> GetTrendingDesignsAsync(int top = 10, CancellationToken ct = default)
            => await _designRepo.GetTrendingAsync(top, ct);

        /// <summary>Creates a new design collection.</summary>
        public async Task<DesignCollection> CreateCollectionAsync(string name, string? season, int? year, string? description, CancellationToken ct = default)
        {
            var collection = new DesignCollection
            {
                CollectionName = name,
                Season         = season,
                Year           = year,
                Description    = description,
                IsActive       = true,
                CreatedAt      = DateTime.UtcNow
            };

            await _uow.Repository<DesignCollection>().AddAsync(collection, ct);
            await _uow.SaveChangesAsync(ct);
            return collection;
        }
    }
}
