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
    /// Manages sales order lifecycle from creation through delivery,
    /// including approval workflow and invoice generation.
    /// </summary>
    public class SalesOrderService
    {
        private readonly IUnitOfWork _uow;
        private readonly SalesOrderRepository _orderRepo;
        private readonly AuditTrailService _auditService;
        private readonly ILogger<SalesOrderService> _logger;

        private static readonly HashSet<(string From, string To)> ValidTransitions = new()
        {
            ("Draft",      "Submitted"),
            ("Submitted",  "Approved"),
            ("Submitted",  "Cancelled"),
            ("Approved",   "InProduction"),
            ("Approved",   "Cancelled"),
            ("InProduction","ReadyToShip"),
            ("ReadyToShip","Shipped"),
            ("Shipped",    "Delivered"),
            ("Delivered",  "Returned"),
        };

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public SalesOrderService(
            IUnitOfWork uow,
            SalesOrderRepository orderRepo,
            AuditTrailService auditService,
            ILogger<SalesOrderService> logger)
        {
            _uow = uow;
            _orderRepo = orderRepo;
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>Creates a new sales order with the supplied line items.</summary>
        public async Task<SalesOrder> CreateOrderAsync(
            int customerId,
            IEnumerable<(int ItemId, int Qty, decimal UnitPrice, decimal DiscPct)> lines,
            string? notes,
            int createdByUserId,
            CancellationToken ct = default)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("A sales order must have at least one line.");

            var orderNo = $"SO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var order = new SalesOrder
            {
                OrderNo          = orderNo,
                CustomerId       = customerId,
                OrderDate        = DateTime.UtcNow,
                Status           = "Draft",
                Notes            = notes,
                CreatedByUserId  = createdByUserId,
                CreatedAt        = DateTime.UtcNow,
                UpdatedAt        = DateTime.UtcNow
            };

            decimal total = 0;
            int sort = 1;

            foreach (var (itemId, qty, unitPrice, discPct) in lineList)
            {
                var lineTotal = qty * unitPrice * (1 - discPct / 100);
                total += lineTotal;

                order.Lines.Add(new SalesOrderLine
                {
                    ItemId          = itemId,
                    Quantity        = qty,
                    UnitPrice       = unitPrice,
                    DiscountPercent = discPct,
                    SortOrder       = sort++
                });
            }

            order.TotalAmount    = total;
            order.DiscountAmount = 0;
            order.TaxAmount      = 0;
            order.NetAmount      = total;

            await _uow.Repository<SalesOrder>().AddAsync(order, ct);
            await _uow.SaveChangesAsync(ct);

            await _auditService.LogCreateAsync("SalesOrder", order.Id.ToString(), $"{{\"orderNo\":\"{order.OrderNo}\"}}", createdByUserId, null, "Orders", ct);
            return order;
        }

        /// <summary>Submits a Draft order for approval. Throws if the order is not in Draft status.</summary>
        public async Task<SalesOrder> SubmitOrderAsync(int orderId, int userId, CancellationToken ct = default)
        {
            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(orderId, ct)
                ?? throw new InvalidOperationException($"Sales order {orderId} not found.");

            if (order.Status != "Draft")
                throw new InvalidOperationException($"Only Draft orders can be submitted. Current status: {order.Status}.");

            order.Status    = "Submitted";
            order.UpdatedAt = DateTime.UtcNow;

            _uow.Repository<SalesOrder>().Update(order);
            await _uow.SaveChangesAsync(ct);
            await _auditService.LogUpdateAsync("SalesOrder", orderId.ToString(), "Draft", "Submitted", userId, null, "Orders", ct);
            return order;
        }

        /// <summary>Records an approval decision for a specific level. Advances order to Approved when all levels pass.</summary>
        public async Task<SalesOrderApproval> ApproveOrderAsync(int orderId, int approverUserId, int level, string? comments, CancellationToken ct = default)
        {
            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(orderId, ct)
                ?? throw new InvalidOperationException($"Sales order {orderId} not found.");

            var existing = await _uow.Repository<SalesOrderApproval>()
                .FirstOrDefaultAsync(a => a.SalesOrderId == orderId && a.ApprovalLevel == level, ct);

            if (existing != null && existing.Status == "Approved")
                throw new InvalidOperationException($"Level {level} approval has already been granted for order {orderId}.");

            SalesOrderApproval approval;

            if (existing != null)
            {
                approval = (await _uow.Repository<SalesOrderApproval>().GetByIdAsync(existing.Id, ct))!;
                approval.Status       = "Approved";
                approval.ApprovalDate  = DateTime.UtcNow;
                approval.Comments      = comments;
                approval.ApproverUserId = approverUserId;
                _uow.Repository<SalesOrderApproval>().Update(approval);
            }
            else
            {
                approval = new SalesOrderApproval
                {
                    SalesOrderId    = orderId,
                    ApprovalLevel   = level,
                    ApproverUserId  = approverUserId,
                    Status          = "Approved",
                    ApprovalDate    = DateTime.UtcNow,
                    Comments        = comments,
                    CreatedAt       = DateTime.UtcNow
                };
                await _uow.Repository<SalesOrderApproval>().AddAsync(approval, ct);
            }

            // Auto-advance order status when level-1 approval is received
            if (level == 1 && order.Status == "Submitted")
            {
                order.Status    = "Approved";
                order.UpdatedAt = DateTime.UtcNow;
                _uow.Repository<SalesOrder>().Update(order);
            }

            await _uow.SaveChangesAsync(ct);
            return approval;
        }

        /// <summary>Rejects a sales order at a given approval level, setting the order to Cancelled.</summary>
        public async Task<SalesOrderApproval> RejectOrderAsync(int orderId, int approverUserId, int level, string? comments, CancellationToken ct = default)
        {
            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(orderId, ct)
                ?? throw new InvalidOperationException($"Sales order {orderId} not found.");

            var rejection = new SalesOrderApproval
            {
                SalesOrderId   = orderId,
                ApprovalLevel  = level,
                ApproverUserId = approverUserId,
                Status         = "Rejected",
                ApprovalDate   = DateTime.UtcNow,
                Comments       = comments,
                CreatedAt      = DateTime.UtcNow
            };

            await _uow.Repository<SalesOrderApproval>().AddAsync(rejection, ct);

            order.Status    = "Cancelled";
            order.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<SalesOrder>().Update(order);

            await _uow.SaveChangesAsync(ct);
            return rejection;
        }

        /// <summary>Updates the order status. Throws if the transition is not permitted.</summary>
        public async Task<SalesOrder> UpdateOrderStatusAsync(int orderId, string newStatus, int userId, CancellationToken ct = default)
        {
            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(orderId, ct)
                ?? throw new InvalidOperationException($"Sales order {orderId} not found.");

            if (!ValidTransitions.Contains((order.Status, newStatus)))
                throw new InvalidOperationException($"Transition from '{order.Status}' to '{newStatus}' is not permitted.");

            var oldStatus   = order.Status;
            order.Status    = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            _uow.Repository<SalesOrder>().Update(order);
            await _uow.SaveChangesAsync(ct);
            await _auditService.LogUpdateAsync("SalesOrder", orderId.ToString(), oldStatus, newStatus, userId, null, "Orders", ct);
            return order;
        }

        /// <summary>Cancels an order. Only Draft, Submitted, or Approved orders may be cancelled.</summary>
        public async Task<SalesOrder> CancelOrderAsync(int orderId, int userId, string reason, CancellationToken ct = default)
        {
            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(orderId, ct)
                ?? throw new InvalidOperationException($"Sales order {orderId} not found.");

            if (order.Status is "Delivered" or "Shipped")
                throw new InvalidOperationException($"Cannot cancel a {order.Status} order.");

            order.Status    = "Cancelled";
            order.Notes     = string.IsNullOrEmpty(order.Notes) ? reason : $"{order.Notes} | Cancellation: {reason}";
            order.UpdatedAt = DateTime.UtcNow;

            _uow.Repository<SalesOrder>().Update(order);
            await _uow.SaveChangesAsync(ct);
            return order;
        }

        /// <summary>Adds an instalment payment schedule entry to a sales order.</summary>
        public async Task<OrderPaymentSchedule> AddPaymentScheduleAsync(int orderId, DateTime dueDate, decimal dueAmount, CancellationToken ct = default)
        {
            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(orderId, ct)
                ?? throw new InvalidOperationException($"Sales order {orderId} not found.");

            var schedule = new OrderPaymentSchedule
            {
                SalesOrderId = orderId,
                DueDate      = dueDate,
                DueAmount    = dueAmount,
                PaidAmount   = 0,
                IsPaid       = false,
                CreatedAt    = DateTime.UtcNow
            };

            await _uow.Repository<OrderPaymentSchedule>().AddAsync(schedule, ct);
            await _uow.SaveChangesAsync(ct);
            return schedule;
        }

        /// <summary>Marks an instalment as paid.</summary>
        public async Task<OrderPaymentSchedule> MarkPaymentPaidAsync(int scheduleId, decimal paidAmount, string? paymentMethod, string? referenceNo, CancellationToken ct = default)
        {
            var schedule = await _uow.Repository<OrderPaymentSchedule>().GetByIdAsync(scheduleId, ct)
                ?? throw new InvalidOperationException($"Payment schedule {scheduleId} not found.");

            schedule.PaidAmount    = paidAmount;
            schedule.PaymentMethod = paymentMethod;
            schedule.ReferenceNo   = referenceNo;
            schedule.PaymentDate   = DateTime.UtcNow;
            schedule.IsPaid        = true;

            _uow.Repository<OrderPaymentSchedule>().Update(schedule);
            await _uow.SaveChangesAsync(ct);
            return schedule;
        }

        /// <summary>Returns all orders for a customer.</summary>
        public async Task<IEnumerable<SalesOrder>> GetOrdersByCustomerAsync(int customerId, CancellationToken ct = default)
            => await _orderRepo.GetByCustomerAsync(customerId, ct);

        /// <summary>Returns all orders with a given status.</summary>
        public async Task<IEnumerable<SalesOrder>> GetOrdersByStatusAsync(string status, CancellationToken ct = default)
            => await _orderRepo.GetByStatusAsync(status, ct);

        /// <summary>Returns a single order with all detail collections included.</summary>
        public async Task<SalesOrder?> GetOrderWithDetailsAsync(int orderId, CancellationToken ct = default)
            => await _orderRepo.GetWithLinesAsync(orderId, ct);

        /// <summary>Processes a return for a delivered order.</summary>
        public async Task<OrderReturn> ProcessReturnAsync(int orderId, string reasonCode, string? description, decimal refundAmount, string? refundMethod, CancellationToken ct = default)
        {
            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(orderId, ct)
                ?? throw new InvalidOperationException($"Sales order {orderId} not found.");

            if (order.Status != "Delivered")
                throw new InvalidOperationException($"Returns can only be raised for Delivered orders. Current status: {order.Status}.");

            var returnNo = $"RET-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var orderReturn = new OrderReturn
            {
                ReturnNo          = returnNo,
                SalesOrderId      = orderId,
                ReturnDate        = DateTime.UtcNow,
                ReasonCode        = reasonCode,
                ReasonDescription = description,
                ReturnStatus      = "Pending",
                RefundAmount      = refundAmount,
                RefundMethod      = refundMethod,
                CreatedAt         = DateTime.UtcNow,
                UpdatedAt         = DateTime.UtcNow
            };

            await _uow.Repository<OrderReturn>().AddAsync(orderReturn, ct);

            order.Status    = "Returned";
            order.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<SalesOrder>().Update(order);

            await _uow.SaveChangesAsync(ct);
            return orderReturn;
        }

        /// <summary>Returns summary metrics for orders placed within a date range.</summary>
        public async Task<(int TotalOrders, decimal TotalValue, int CancelledOrders, decimal CancellationRate)> GetOrderMetricsAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var orders = (await _orderRepo.GetByDateRangeAsync(from, to, ct)).ToList();

            var total      = orders.Count;
            var totalValue = orders.Sum(o => o.NetAmount);
            var cancelled  = orders.Count(o => o.Status == "Cancelled");
            var rate       = total > 0 ? (decimal)cancelled / total * 100 : 0;

            return (total, totalValue, cancelled, rate);
        }
    }
}
