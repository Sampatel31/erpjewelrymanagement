using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for PurchaseOrder with supplier, status, date, Include, and overdue queries.</summary>
    public class PurchaseOrderRepository : GenericRepository<PurchaseOrder>
    {
        public PurchaseOrderRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all purchase orders for a given supplier.</summary>
        public async Task<IEnumerable<PurchaseOrder>> GetBySupplierAsync(int supplierId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(po => po.SupplierId == supplierId)
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync(ct);

        /// <summary>Returns all purchase orders with the given status.</summary>
        public async Task<IEnumerable<PurchaseOrder>> GetByStatusAsync(string status, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(po => po.Status == status)
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync(ct);

        /// <summary>Returns purchase orders placed within a date range.</summary>
        public async Task<IEnumerable<PurchaseOrder>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(po => po.OrderDate >= from && po.OrderDate <= to)
                .OrderBy(po => po.OrderDate)
                .ToListAsync(ct);

        /// <summary>Returns a purchase order with its lines included.</summary>
        public async Task<PurchaseOrder?> GetWithLinesAsync(int poId, CancellationToken ct = default)
            => await _dbSet
                .Include(po => po.Lines)
                .FirstOrDefaultAsync(po => po.Id == poId, ct);

        /// <summary>Returns purchase orders past their expected delivery date that have not been received or cancelled.</summary>
        public async Task<IEnumerable<PurchaseOrder>> GetOverdueAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return await _dbSet.AsNoTracking()
                .Where(po => po.ExpectedDeliveryDate.HasValue
                          && po.ExpectedDeliveryDate.Value < now
                          && po.Status != "Received"
                          && po.Status != "Cancelled")
                .OrderBy(po => po.ExpectedDeliveryDate)
                .ToListAsync(ct);
        }
    }
}
