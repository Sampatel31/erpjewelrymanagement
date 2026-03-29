using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for ReportTemplate and ReportSchedule with type and scheduling queries.</summary>
    public class ReportRepository : GenericRepository<ReportTemplate>
    {
        private readonly ShingarContext _ctx;

        public ReportRepository(ShingarContext context) : base(context)
        {
            _ctx = context;
        }

        /// <summary>Returns all active report templates.</summary>
        public async Task<IEnumerable<ReportTemplate>> GetActiveTemplatesAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(t => t.IsActive)
                .ToListAsync(ct);

        /// <summary>Returns active report templates of a specific type.</summary>
        public async Task<IEnumerable<ReportTemplate>> GetByTypeAsync(string reportType, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(t => t.ReportType == reportType && t.IsActive)
                .ToListAsync(ct);

        /// <summary>Returns all active report schedules.</summary>
        public async Task<IEnumerable<ReportSchedule>> GetScheduledReportsAsync(CancellationToken ct = default)
            => await _ctx.ReportSchedules.AsNoTracking()
                .Where(s => s.IsActive)
                .Include(s => s.Template)
                .ToListAsync(ct);

        /// <summary>Returns active schedules whose next run date is due (NextRunDate &lt;= now).</summary>
        public async Task<IEnumerable<ReportSchedule>> GetDueSchedulesAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return await _ctx.ReportSchedules.AsNoTracking()
                .Where(s => s.IsActive && s.NextRunDate <= now)
                .Include(s => s.Template)
                .ToListAsync(ct);
        }
    }
}
