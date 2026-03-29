using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for AuditLog with entity, user, date, operation, and module queries.</summary>
    public class AuditLogRepository : GenericRepository<AuditLog>
    {
        public AuditLogRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all audit records for a specific entity by name and ID.</summary>
        public async Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityName, string entityId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(a => a.EntityName == entityName && a.EntityId == entityId)
                .OrderBy(a => a.Timestamp)
                .ToListAsync(ct);

        /// <summary>Returns audit logs for a user within a date range.</summary>
        public async Task<IEnumerable<AuditLog>> GetByUserAsync(int userId, DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(a => a.UserId == userId && a.Timestamp >= from && a.Timestamp <= to)
                .OrderBy(a => a.Timestamp)
                .ToListAsync(ct);

        /// <summary>Returns all audit logs within a date range.</summary>
        public async Task<IEnumerable<AuditLog>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(a => a.Timestamp >= from && a.Timestamp <= to)
                .OrderBy(a => a.Timestamp)
                .ToListAsync(ct);

        /// <summary>Returns audit logs filtered by operation type within a date range.</summary>
        public async Task<IEnumerable<AuditLog>> GetByOperationTypeAsync(string operationType, DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(a => a.OperationType == operationType && a.Timestamp >= from && a.Timestamp <= to)
                .OrderBy(a => a.Timestamp)
                .ToListAsync(ct);

        /// <summary>Returns audit logs for a specific application module.</summary>
        public async Task<IEnumerable<AuditLog>> GetByModuleAsync(string module, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(a => a.Module == module)
                .OrderBy(a => a.Timestamp)
                .ToListAsync(ct);
    }
}
