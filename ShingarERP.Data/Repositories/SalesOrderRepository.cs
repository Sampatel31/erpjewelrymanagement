using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data.Repositories
{
    /// <summary>Repository for SalesOrder with customer, status, date, value, and Include queries.</summary>
    public class SalesOrderRepository : GenericRepository<SalesOrder>
    {
        public SalesOrderRepository(ShingarContext context) : base(context) { }

        /// <summary>Returns all sales orders for a given customer.</summary>
        public async Task<IEnumerable<SalesOrder>> GetByCustomerAsync(int customerId, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(so => so.CustomerId == customerId)
                .OrderByDescending(so => so.OrderDate)
                .ToListAsync(ct);

        /// <summary>Returns all sales orders with the given status.</summary>
        public async Task<IEnumerable<SalesOrder>> GetByStatusAsync(string status, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(so => so.Status == status)
                .OrderByDescending(so => so.OrderDate)
                .ToListAsync(ct);

        /// <summary>Returns sales orders placed within a date range.</summary>
        public async Task<IEnumerable<SalesOrder>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(so => so.OrderDate >= from && so.OrderDate <= to)
                .OrderBy(so => so.OrderDate)
                .ToListAsync(ct);

        /// <summary>Returns submitted sales orders that are awaiting approval.</summary>
        public async Task<IEnumerable<SalesOrder>> GetPendingApprovalAsync(CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(so => so.Status == "Submitted")
                .OrderBy(so => so.OrderDate)
                .ToListAsync(ct);

        /// <summary>Returns sales orders whose net amount falls within a value range.</summary>
        public async Task<IEnumerable<SalesOrder>> GetByValueRangeAsync(decimal minValue, decimal maxValue, CancellationToken ct = default)
            => await _dbSet.AsNoTracking()
                .Where(so => so.NetAmount >= minValue && so.NetAmount <= maxValue)
                .OrderByDescending(so => so.NetAmount)
                .ToListAsync(ct);

        /// <summary>Returns a single sales order with its lines, approvals, and payment schedules included.</summary>
        public async Task<SalesOrder?> GetWithLinesAsync(int orderId, CancellationToken ct = default)
            => await _dbSet
                .Include(so => so.Lines)
                .Include(so => so.Approvals)
                .Include(so => so.PaymentSchedules)
                .FirstOrDefaultAsync(so => so.Id == orderId, ct);
    }
}
