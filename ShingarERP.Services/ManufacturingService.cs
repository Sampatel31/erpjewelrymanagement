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
    /// Manages manufacturing job card lifecycle from creation through QC completion.
    /// Enforces stage-based state machine workflow and labor/material cost tracking.
    /// </summary>
    public class ManufacturingService
    {
        private readonly IUnitOfWork _uow;
        private readonly JobCardRepository _jobCardRepo;
        private readonly ILogger<ManufacturingService> _logger;

        private static readonly HashSet<(string From, string To)> ValidTransitions = new()
        {
            ("Draft",           "MaterialAllotment"),
            ("MaterialAllotment","Casting"),
            ("Casting",         "Setting"),
            ("Setting",         "Finishing"),
            ("Finishing",       "QCGate"),
            ("QCGate",          "Ready"),
            ("QCGate",          "Rework"),
            ("Rework",          "QCGate"),
            ("Draft",           "Cancelled"),
            ("MaterialAllotment","Cancelled"),
        };

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public ManufacturingService(IUnitOfWork uow, JobCardRepository jobCardRepo, ILogger<ManufacturingService> logger)
        {
            _uow = uow;
            _jobCardRepo = jobCardRepo;
            _logger = logger;
        }

        /// <summary>Creates a new job card from a design with initial Draft status.</summary>
        public async Task<JobCard> CreateJobCardAsync(
            int? salesOrderId,
            int? designId,
            int? karigarId,
            DateTime dueDate,
            decimal estimatedHours,
            decimal estimatedCost,
            string? instructions,
            int createdByUserId,
            CancellationToken ct = default)
        {
            var jobCardNo = $"JC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var jobCard = new JobCard
            {
                JobCardNo      = jobCardNo,
                SalesOrderId   = salesOrderId,
                DesignId       = designId,
                KarigarId      = karigarId,
                Status         = "Draft",
                IssuedDate     = DateTime.UtcNow,
                DueDate        = dueDate,
                EstimatedHours = estimatedHours,
                EstimatedCost  = estimatedCost,
                Instructions   = instructions,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow
            };

            // Create default stages
            var stageNames = new[] { "MaterialAllotment", "Casting", "Setting", "Finishing", "QCGate" };
            for (int i = 0; i < stageNames.Length; i++)
            {
                jobCard.Stages.Add(new JobCardStage
                {
                    StageName  = stageNames[i],
                    StageOrder = i + 1,
                    Status     = "Pending"
                });
            }

            await _uow.Repository<JobCard>().AddAsync(jobCard, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Job card {JobCardNo} created (Id={Id})", jobCardNo, jobCard.Id);
            return jobCard;
        }

        /// <summary>Advances a job card to the next status stage.</summary>
        public async Task<JobCard> AdvanceStageAsync(int jobCardId, string toStatus, int userId, string? reason = null, CancellationToken ct = default)
        {
            var jobCard = await _jobCardRepo.GetWithDetailsAsync(jobCardId, ct)
                ?? throw new InvalidOperationException($"Job card {jobCardId} not found.");

            if (!ValidTransitions.Contains((jobCard.Status, toStatus)))
                throw new InvalidOperationException($"Invalid transition from '{jobCard.Status}' to '{toStatus}'.");

            var history = new JobCardHistory
            {
                JobCardId       = jobCardId,
                FromStatus      = jobCard.Status,
                ToStatus        = toStatus,
                ChangedByUserId = userId,
                Reason          = reason,
                ChangedAt       = DateTime.UtcNow
            };

            await _uow.Repository<JobCardHistory>().AddAsync(history, ct);

            jobCard.Status    = toStatus;
            jobCard.UpdatedAt = DateTime.UtcNow;

            if (toStatus == "Ready")
                jobCard.CompletedDate = DateTime.UtcNow;

            // Mark the corresponding stage as completed
            var stage = jobCard.Stages.FirstOrDefault(s => s.StageName == toStatus);
            if (stage != null)
            {
                stage.Status      = "Completed";
                stage.CompletedAt = DateTime.UtcNow;
            }

            _uow.Repository<JobCard>().Update(jobCard);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Job card {JobCardNo} advanced to '{Status}'", jobCard.JobCardNo, toStatus);
            return jobCard;
        }

        /// <summary>Records actual labor hours for a karigar on a job card.</summary>
        public async Task<JobCardLabor> RecordLaborAsync(int jobCardId, int karigarId, decimal hoursWorked, decimal ratePerHour, DateTime workDate, string? notes = null, CancellationToken ct = default)
        {
            if (hoursWorked <= 0)
                throw new InvalidOperationException("Hours worked must be positive.");

            var labor = new JobCardLabor
            {
                JobCardId   = jobCardId,
                KarigarId   = karigarId,
                HoursWorked = hoursWorked,
                RatePerHour = ratePerHour,
                LaborCost   = hoursWorked * ratePerHour,
                WorkDate    = workDate,
                Notes       = notes
            };

            await _uow.Repository<JobCardLabor>().AddAsync(labor, ct);

            // Update actual cost on job card
            var jobCard = await _jobCardRepo.GetByIdAsync(jobCardId, ct)
                ?? throw new InvalidOperationException($"Job card {jobCardId} not found.");
            jobCard.ActualCost += labor.LaborCost;
            jobCard.UpdatedAt   = DateTime.UtcNow;
            _uow.Repository<JobCard>().Update(jobCard);

            await _uow.SaveChangesAsync(ct);
            return labor;
        }

        /// <summary>Records material consumption for a job card.</summary>
        public async Task<JobCardMaterial> RecordMaterialAsync(int jobCardId, string materialType, decimal actualQty, decimal estimatedQty, decimal costPerUnit, string unit = "g", string? description = null, CancellationToken ct = default)
        {
            if (actualQty < 0)
                throw new InvalidOperationException("Actual quantity cannot be negative.");

            var material = new JobCardMaterial
            {
                JobCardId           = jobCardId,
                MaterialType        = materialType,
                MaterialDescription = description,
                EstimatedQty        = estimatedQty,
                ActualQty           = actualQty,
                Unit                = unit,
                CostPerUnit         = costPerUnit,
                TotalCost           = actualQty * costPerUnit
            };

            await _uow.Repository<JobCardMaterial>().AddAsync(material, ct);

            var jobCard = await _jobCardRepo.GetByIdAsync(jobCardId, ct)
                ?? throw new InvalidOperationException($"Job card {jobCardId} not found.");
            jobCard.ActualCost += material.TotalCost;
            jobCard.UpdatedAt   = DateTime.UtcNow;
            _uow.Repository<JobCard>().Update(jobCard);

            await _uow.SaveChangesAsync(ct);
            return material;
        }

        /// <summary>Calculates total labor cost for a job card.</summary>
        public async Task<decimal> CalculateLaborCostAsync(int jobCardId, CancellationToken ct = default)
        {
            var jobCard = await _jobCardRepo.GetWithDetailsAsync(jobCardId, ct)
                ?? throw new InvalidOperationException($"Job card {jobCardId} not found.");
            return jobCard.Labor.Sum(l => l.LaborCost);
        }

        /// <summary>Returns overdue job cards.</summary>
        public async Task<IEnumerable<JobCard>> GetOverdueJobCardsAsync(CancellationToken ct = default)
            => await _jobCardRepo.GetOverdueAsync(ct);
    }
}
