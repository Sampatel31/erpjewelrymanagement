using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for OrderFulfillment with status, sales order, and delivery queries.</summary>
    public class OrderFulfillmentRepository : GenericRepository<OrderFulfillment>
    {
        public OrderFulfillmentRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns fulfillment records with the given status.</summary>
        public async Task<IEnumerable<OrderFulfillment>> GetByStatusAsync(string status, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(f => f.Status == status)
                .ToListAsync(ct);

        /// <summary>Returns the fulfillment record for a specific sales order.</summary>
        public async Task<OrderFulfillment?> GetBySalesOrderAsync(int salesOrderId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(f => f.SalesOrderId == salesOrderId, ct);

        /// <summary>Returns fulfillments that have not yet been shipped (status Pending, Packing, or Packed).</summary>
        public async Task<IEnumerable<OrderFulfillment>> GetPendingShipmentAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(f => f.Status == "Pending" || f.Status == "Packing" || f.Status == "Packed")
                .ToListAsync(ct);

        /// <summary>Returns fulfillments delivered today.</summary>
        public async Task<IEnumerable<OrderFulfillment>> GetDeliveredTodayAsync(CancellationToken ct = default)
        {
            var today = DateTime.UtcNow.Date;
            return await _dbSet.AsNoTracking()
                .Where(f => f.Status == "Delivered"
                         && f.DeliveryDate.HasValue
                         && f.DeliveryDate.Value.Date == today)
                .ToListAsync(ct);
        }
    }
}
