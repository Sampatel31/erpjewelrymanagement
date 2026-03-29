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
    /// Manages melting batch lifecycle from creation through distribution.
    /// Validates alloy compositions, calculates melting loss, and tracks lot traceability.
    /// </summary>
    public class MeltingService
    {
        private readonly IUnitOfWork _uow;
        private readonly MeltingBatchRepository _batchRepo;
        private readonly ILogger<MeltingService> _logger;

        private static readonly HashSet<(string From, string To)> ValidTransitions = new()
        {
            ("Pending",  "Melting"),
            ("Melting",  "Complete"),
            ("Complete", "Distributed"),
            ("Pending",  "Cancelled"),
        };

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public MeltingService(IUnitOfWork uow, MeltingBatchRepository batchRepo, ILogger<MeltingService> logger)
        {
            _uow = uow;
            _batchRepo = batchRepo;
            _logger = logger;
        }

        /// <summary>Creates a new melting batch with optional input lots and alloy composition.</summary>
        public async Task<MeltingBatch> CreateBatchAsync(
            string metalType,
            decimal grossWeight,
            IEnumerable<(int? MetalLotId, string? LotNumber, decimal WeightUsed)> inputs,
            IEnumerable<(string MetalType, string? Purity, decimal Proportion)> compositions,
            string? remarks = null,
            CancellationToken ct = default)
        {
            var compList = compositions.ToList();
            var totalProportion = compList.Sum(c => c.Proportion);
            if (Math.Abs(totalProportion - 100m) > 0.01m)
                throw new InvalidOperationException($"Alloy composition proportions must sum to 100. Current sum: {totalProportion}.");

            var batchNo = $"MB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var batch = new MeltingBatch
            {
                BatchNo    = batchNo,
                MetalType  = metalType,
                GrossWeight = grossWeight,
                Status     = "Pending",
                BatchDate  = DateTime.UtcNow,
                Remarks    = remarks,
                CreatedAt  = DateTime.UtcNow
            };

            foreach (var (lotId, lotNo, weight) in inputs)
                batch.Inputs.Add(new MeltingInput { MetalLotId = lotId, LotNumber = lotNo, WeightUsed = weight });

            int version = 1;
            foreach (var (mt, purity, prop) in compList)
                batch.AlloyCompositions.Add(new AlloyComposition { MetalType = mt, Purity = purity, Proportion = prop, Version = version });

            await _uow.Repository<MeltingBatch>().AddAsync(batch, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Melting batch {BatchNo} created (Id={Id})", batchNo, batch.Id);
            return batch;
        }

        /// <summary>Advances batch status through the workflow state machine.</summary>
        public async Task<MeltingBatch> AdvanceBatchStatusAsync(int batchId, string toStatus, CancellationToken ct = default)
        {
            var batch = await _batchRepo.GetByIdAsync(batchId, ct)
                ?? throw new InvalidOperationException($"Melting batch {batchId} not found.");

            if (!ValidTransitions.Contains((batch.Status, toStatus)))
                throw new InvalidOperationException($"Invalid transition from '{batch.Status}' to '{toStatus}'.");

            batch.Status = toStatus;
            if (toStatus == "Complete")
                batch.CompletedAt = DateTime.UtcNow;

            _uow.Repository<MeltingBatch>().Update(batch);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Melting batch {BatchNo} status → '{Status}'", batch.BatchNo, toStatus);
            return batch;
        }

        /// <summary>Records net weight after melting and calculates melting loss percentage.</summary>
        public async Task<MeltingBatch> RecordNetWeightAsync(int batchId, decimal netWeight, CancellationToken ct = default)
        {
            if (netWeight <= 0)
                throw new InvalidOperationException("Net weight must be positive.");

            var batch = await _batchRepo.GetByIdAsync(batchId, ct)
                ?? throw new InvalidOperationException($"Melting batch {batchId} not found.");

            if (netWeight > batch.GrossWeight)
                throw new InvalidOperationException("Net weight cannot exceed gross weight.");

            batch.NetWeight          = netWeight;
            batch.MeltingLossPercent = batch.GrossWeight > 0
                ? Math.Round((batch.GrossWeight - netWeight) / batch.GrossWeight * 100m, 4)
                : 0;

            _uow.Repository<MeltingBatch>().Update(batch);
            await _uow.SaveChangesAsync(ct);
            return batch;
        }

        /// <summary>Validates that alloy compositions for a batch sum to 100%.</summary>
        public async Task<bool> ValidateCompositionAsync(int batchId, CancellationToken ct = default)
        {
            var batch = await _batchRepo.GetWithDetailsAsync(batchId, ct)
                ?? throw new InvalidOperationException($"Melting batch {batchId} not found.");
            var total = batch.AlloyCompositions.Sum(c => c.Proportion);
            return Math.Abs(total - 100m) <= 0.01m;
        }
    }
}
