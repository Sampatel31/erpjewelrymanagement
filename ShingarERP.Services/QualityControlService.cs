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
    /// Manages QC inspection workflow, defect recording, pass/fail decisions,
    /// and craftsman quality metric aggregation.
    /// </summary>
    public class QualityControlService
    {
        private readonly IUnitOfWork _uow;
        private readonly QCRecordRepository _qcRepo;
        private readonly ILogger<QualityControlService> _logger;

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public QualityControlService(IUnitOfWork uow, QCRecordRepository qcRepo, ILogger<QualityControlService> logger)
        {
            _uow = uow;
            _qcRepo = qcRepo;
            _logger = logger;
        }

        /// <summary>Creates a new QC inspection record for a job card or finished good.</summary>
        public async Task<QCRecord> CreateInspectionAsync(
            int? jobCardId, int? finishedGoodId, int inspectorUserId, int? inspectedByKarigarId,
            decimal qualityScore, string? defectNotes = null, string? remarks = null,
            CancellationToken ct = default)
        {
            if (qualityScore < 0 || qualityScore > 100)
                throw new InvalidOperationException("Quality score must be between 0 and 100.");

            var qcNo = $"QC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var record = new QCRecord
            {
                QCNo                   = qcNo,
                JobCardId              = jobCardId,
                FinishedGoodId         = finishedGoodId,
                InspectorUserId        = inspectorUserId,
                InspectedByKarigarId   = inspectedByKarigarId,
                Result                 = "Pending",
                QualityScore           = qualityScore,
                DefectNotes            = defectNotes,
                Remarks                = remarks,
                InspectionDate         = DateTime.UtcNow,
                CreatedAt              = DateTime.UtcNow
            };

            await _uow.Repository<QCRecord>().AddAsync(record, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("QC inspection {QCNo} created (Id={Id})", qcNo, record.Id);
            return record;
        }

        /// <summary>Records a defect against a QC inspection record.</summary>
        public async Task<QCDefect> RecordDefectAsync(
            int qcRecordId, string defectCode, string description, string severity, decimal remedyCost = 0,
            CancellationToken ct = default)
        {
            var validSeverities = new[] { "Critical", "Major", "Minor" };
            if (!validSeverities.Contains(severity))
                throw new InvalidOperationException($"Invalid severity '{severity}'. Must be Critical, Major, or Minor.");

            var defect = new QCDefect
            {
                QCRecordId  = qcRecordId,
                DefectCode  = defectCode,
                Description = description,
                Severity    = severity,
                RemedyCost  = remedyCost,
                IsResolved  = false
            };

            await _uow.Repository<QCDefect>().AddAsync(defect, ct);
            await _uow.SaveChangesAsync(ct);
            return defect;
        }

        /// <summary>Records the final pass/fail decision on a QC inspection.</summary>
        public async Task<QCRecord> RecordDecisionAsync(int qcRecordId, string result, string? remarks = null, CancellationToken ct = default)
        {
            var validResults = new[] { "Pass", "Fail", "Conditional" };
            if (!validResults.Contains(result))
                throw new InvalidOperationException($"Invalid QC result '{result}'. Must be Pass, Fail, or Conditional.");

            var record = await _qcRepo.GetWithDefectsAsync(qcRecordId, ct)
                ?? throw new InvalidOperationException($"QC record {qcRecordId} not found.");

            // A record with Critical defects cannot be passed
            if (result == "Pass" && record.Defects.Any(d => d.Severity == "Critical" && !d.IsResolved))
                throw new InvalidOperationException("Cannot pass inspection: unresolved Critical defects exist.");

            record.Result  = result;
            record.Remarks = remarks ?? record.Remarks;
            _uow.Repository<QCRecord>().Update(record);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("QC record {QCNo} decision: {Result}", record.QCNo, result);
            return record;
        }

        /// <summary>Calculates pass rate (%) for QC records in a date range.</summary>
        public async Task<decimal> GetPassRateAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var records = (await _qcRepo.GetByDateRangeAsync(from, to, ct)).ToList();
            if (!records.Any()) return 0;
            var passed = records.Count(r => r.Result == "Pass");
            return Math.Round(passed * 100m / records.Count, 2);
        }

        /// <summary>Returns QC records for a specific job card.</summary>
        public async Task<IEnumerable<QCRecord>> GetJobCardInspectionsAsync(int jobCardId, CancellationToken ct = default)
            => await _qcRepo.GetByJobCardAsync(jobCardId, ct);

        /// <summary>Returns failed inspections requiring rework.</summary>
        public async Task<IEnumerable<QCRecord>> GetFailedInspectionsAsync(CancellationToken ct = default)
            => await _qcRepo.GetByResultAsync("Fail", ct);
    }
}
