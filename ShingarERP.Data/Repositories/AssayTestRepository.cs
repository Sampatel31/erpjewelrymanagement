using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for AssayTest with metal lot, date, and certificate queries.</summary>
    public class AssayTestRepository : GenericRepository<AssayTest>
    {
        public AssayTestRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns assay tests for a specific metal lot.</summary>
        public async Task<IEnumerable<AssayTest>> GetByMetalLotAsync(int metalLotId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(a => a.MetalLotId == metalLotId)
                .OrderByDescending(a => a.TestDate)
                .ToListAsync(ct);

        /// <summary>Returns assay tests within a date range.</summary>
        public async Task<IEnumerable<AssayTest>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(a => a.TestDate >= from && a.TestDate <= to)
                .OrderBy(a => a.TestDate)
                .ToListAsync(ct);

        /// <summary>Returns an assay test by certificate number.</summary>
        public async Task<AssayTest?> GetByCertificateAsync(string certificateNo, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(a => a.CertificateNo == certificateNo, ct);
    }
}
