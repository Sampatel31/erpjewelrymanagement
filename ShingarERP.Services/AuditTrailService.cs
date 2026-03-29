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
    /// Comprehensive audit trail logging for compliance and data integrity.
    /// Supports SOX, FEMA, and GST regulation audit requirements.
    /// </summary>
    public class AuditTrailService
    {
        private readonly IUnitOfWork _uow;
        private readonly AuditLogRepository _auditRepo;
        private readonly ILogger<AuditTrailService> _logger;

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public AuditTrailService(
            IUnitOfWork uow,
            AuditLogRepository auditRepo,
            ILogger<AuditTrailService> logger)
        {
            _uow = uow;
            _auditRepo = auditRepo;
            _logger = logger;
        }

        /// <summary>Logs a generic audit event with full context.</summary>
        public async Task<AuditLog> LogAsync(
            string entityName,
            string entityId,
            string operationType,
            string? oldValues,
            string? newValues,
            int? userId,
            string? userName,
            string? ipAddress,
            string? module,
            CancellationToken ct = default)
        {
            var log = new AuditLog
            {
                EntityName      = entityName,
                EntityId        = entityId,
                OperationType   = operationType,
                OldValues       = oldValues,
                NewValues       = newValues,
                UserId          = userId,
                UserName        = userName,
                IpAddress       = ipAddress,
                Module          = module,
                CorrelationId   = Guid.NewGuid().ToString("N")[..8],
                Timestamp       = DateTime.UtcNow
            };

            await _uow.Repository<AuditLog>().AddAsync(log, ct);
            await _uow.SaveChangesAsync(ct);
            return log;
        }

        /// <summary>Logs a Create operation for an entity.</summary>
        public async Task<AuditLog> LogCreateAsync(string entityName, string entityId, string? newValues, int? userId, string? userName, string? module, CancellationToken ct = default)
            => await LogAsync(entityName, entityId, "Create", null, newValues, userId, userName, null, module, ct);

        /// <summary>Logs an Update operation with before/after values.</summary>
        public async Task<AuditLog> LogUpdateAsync(string entityName, string entityId, string? oldValues, string? newValues, int? userId, string? userName, string? module, CancellationToken ct = default)
            => await LogAsync(entityName, entityId, "Update", oldValues, newValues, userId, userName, null, module, ct);

        /// <summary>Logs a Delete operation with the values that were removed.</summary>
        public async Task<AuditLog> LogDeleteAsync(string entityName, string entityId, string? oldValues, int? userId, string? userName, string? module, CancellationToken ct = default)
            => await LogAsync(entityName, entityId, "Delete", oldValues, null, userId, userName, null, module, ct);

        /// <summary>Returns the full audit history for a specific entity.</summary>
        public async Task<IEnumerable<AuditLog>> GetEntityHistoryAsync(string entityName, string entityId, CancellationToken ct = default)
            => await _auditRepo.GetByEntityAsync(entityName, entityId, ct);

        /// <summary>Returns audit activity for a user within a date range.</summary>
        public async Task<IEnumerable<AuditLog>> GetUserActivityAsync(int userId, DateTime from, DateTime to, CancellationToken ct = default)
            => await _auditRepo.GetByUserAsync(userId, from, to, ct);

        /// <summary>Returns all audit records within a date range, optionally filtered by module.</summary>
        public async Task<IEnumerable<AuditLog>> GetAuditReportAsync(DateTime from, DateTime to, string? module = null, CancellationToken ct = default)
        {
            var logs = await _auditRepo.GetByDateRangeAsync(from, to, ct);
            return module == null ? logs : logs.Where(l => l.Module == module);
        }

        /// <summary>
        /// Verifies data integrity by confirming there are no timestamp gaps for an entity
        /// in a given date range. Returns true if the audit trail is continuous.
        /// </summary>
        public async Task<bool> VerifyDataIntegrityAsync(string entityName, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var logs = (await _auditRepo.GetByDateRangeAsync(from, to, ct))
                .Where(l => l.EntityName == entityName)
                .OrderBy(l => l.Timestamp)
                .ToList();

            if (!logs.Any())
                return true;

            // Verify no single gap exceeds 24 hours between consecutive Create/Update/Delete events
            for (int i = 1; i < logs.Count; i++)
            {
                var gap = logs[i].Timestamp - logs[i - 1].Timestamp;
                if (gap.TotalHours > 24)
                    return false;
            }

            return true;
        }

        /// <summary>Returns compliance-relevant audit logs (Create/Update/Delete) within a period.</summary>
        public async Task<IEnumerable<AuditLog>> GetComplianceReportAsync(string reportType, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var logs = await _auditRepo.GetByDateRangeAsync(from, to, ct);
            return logs.Where(l => l.OperationType != "Read");
        }

        /// <summary>Returns all audit activity within a module for a date range.</summary>
        public async Task<IEnumerable<AuditLog>> GetModuleActivityAsync(string module, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var logs = await _auditRepo.GetByModuleAsync(module, ct);
            return logs.Where(l => l.Timestamp >= from && l.Timestamp <= to);
        }
    }
}
