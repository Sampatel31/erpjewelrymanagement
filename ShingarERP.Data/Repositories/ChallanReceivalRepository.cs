using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for ChallanReceival with QC status and reconciliation queries.</summary>
    public class ChallanReceivalRepository : GenericRepository<ChallanReceival>
    {
        public ChallanReceivalRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns receivals by QC status.</summary>
        public async Task<IEnumerable<ChallanReceival>> GetByQCStatusAsync(string qcStatus, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(r => r.QCStatus == qcStatus)
                .OrderByDescending(r => r.ReceivalDate)
                .ToListAsync(ct);

        /// <summary>Returns all receivals for a specific challan.</summary>
        public async Task<IEnumerable<ChallanReceival>> GetByChallanAsync(int challanId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(r => r.ChallanId == challanId)
                .OrderByDescending(r => r.ReceivalDate)
                .ToListAsync(ct);
    }
}
