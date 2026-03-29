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
    /// Manages order packing, shipment, delivery, and return logistics.
    /// </summary>
    public class OrderFulfillmentService
    {
        private readonly IUnitOfWork _uow;
        private readonly OrderFulfillmentRepository _fulfillmentRepo;
        private readonly AuditTrailService _auditService;
        private readonly ILogger<OrderFulfillmentService> _logger;

        private int _slipSequence;
        private readonly object _seqLock = new();

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public OrderFulfillmentService(
            IUnitOfWork uow,
            OrderFulfillmentRepository fulfillmentRepo,
            AuditTrailService auditService,
            ILogger<OrderFulfillmentService> logger)
        {
            _uow = uow;
            _fulfillmentRepo = fulfillmentRepo;
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>Creates a fulfilment record for an Approved or ReadyToShip order.</summary>
        public async Task<OrderFulfillment> CreateFulfillmentAsync(int salesOrderId, CancellationToken ct = default)
        {
            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(salesOrderId, ct)
                ?? throw new InvalidOperationException($"Sales order {salesOrderId} not found.");

            if (order.Status != "Approved" && order.Status != "ReadyToShip")
                throw new InvalidOperationException($"Fulfilment can only be created for Approved or ReadyToShip orders. Current status: {order.Status}.");

            var existing = await _fulfillmentRepo.GetBySalesOrderAsync(salesOrderId, ct);
            if (existing != null)
                throw new InvalidOperationException($"A fulfilment record already exists for sales order {salesOrderId}.");

            var fulfillment = new OrderFulfillment
            {
                SalesOrderId = salesOrderId,
                Status       = "Pending",
                CreatedAt    = DateTime.UtcNow,
                UpdatedAt    = DateTime.UtcNow
            };

            await _uow.Repository<OrderFulfillment>().AddAsync(fulfillment, ct);
            await _uow.SaveChangesAsync(ct);
            return fulfillment;
        }

        /// <summary>Starts packing a fulfillment (Pending → Packing).</summary>
        public async Task<OrderFulfillment> StartPackingAsync(int fulfillmentId, int userId, CancellationToken ct = default)
        {
            var f = await _uow.Repository<OrderFulfillment>().GetByIdAsync(fulfillmentId, ct)
                ?? throw new InvalidOperationException($"Fulfilment {fulfillmentId} not found.");

            if (f.Status != "Pending")
                throw new InvalidOperationException($"Only Pending fulfilments can be packed. Current status: {f.Status}.");

            f.Status    = "Packing";
            f.UpdatedAt = DateTime.UtcNow;

            _uow.Repository<OrderFulfillment>().Update(f);
            await _uow.SaveChangesAsync(ct);
            return f;
        }

        /// <summary>Completes packing (Packing → Packed) and updates sales order status to ReadyToShip.</summary>
        public async Task<OrderFulfillment> CompletePackingAsync(int fulfillmentId, string packingSlipNo, int userId, CancellationToken ct = default)
        {
            var f = await _uow.Repository<OrderFulfillment>().GetByIdAsync(fulfillmentId, ct)
                ?? throw new InvalidOperationException($"Fulfilment {fulfillmentId} not found.");

            if (f.Status != "Packing")
                throw new InvalidOperationException($"Only Packing fulfilments can be completed. Current status: {f.Status}.");

            f.Status        = "Packed";
            f.PackingSlipNo = packingSlipNo;
            f.PackingDate   = DateTime.UtcNow;
            f.UpdatedAt     = DateTime.UtcNow;

            _uow.Repository<OrderFulfillment>().Update(f);

            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(f.SalesOrderId, ct);
            if (order != null)
            {
                order.Status    = "ReadyToShip";
                order.UpdatedAt = DateTime.UtcNow;
                _uow.Repository<SalesOrder>().Update(order);
            }

            await _uow.SaveChangesAsync(ct);
            return f;
        }

        /// <summary>Ships the order (Packed → Shipped) and updates sales order to Shipped.</summary>
        public async Task<OrderFulfillment> ShipOrderAsync(int fulfillmentId, string trackingNo, string shippingProvider, int userId, CancellationToken ct = default)
        {
            var f = await _uow.Repository<OrderFulfillment>().GetByIdAsync(fulfillmentId, ct)
                ?? throw new InvalidOperationException($"Fulfilment {fulfillmentId} not found.");

            if (f.Status != "Packed")
                throw new InvalidOperationException($"Only Packed fulfilments can be shipped. Current status: {f.Status}.");

            f.Status              = "Shipped";
            f.ShipmentTrackingNo  = trackingNo;
            f.ShippingProvider    = shippingProvider;
            f.ShippedDate         = DateTime.UtcNow;
            f.UpdatedAt           = DateTime.UtcNow;

            _uow.Repository<OrderFulfillment>().Update(f);

            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(f.SalesOrderId, ct);
            if (order != null)
            {
                order.Status    = "Shipped";
                order.UpdatedAt = DateTime.UtcNow;
                _uow.Repository<SalesOrder>().Update(order);
            }

            await _uow.SaveChangesAsync(ct);
            return f;
        }

        /// <summary>Confirms delivery (Shipped → Delivered) and updates sales order to Delivered.</summary>
        public async Task<OrderFulfillment> ConfirmDeliveryAsync(int fulfillmentId, string? receivedByName, int userId, CancellationToken ct = default)
        {
            var f = await _uow.Repository<OrderFulfillment>().GetByIdAsync(fulfillmentId, ct)
                ?? throw new InvalidOperationException($"Fulfilment {fulfillmentId} not found.");

            if (f.Status != "Shipped")
                throw new InvalidOperationException($"Only Shipped fulfilments can be confirmed as delivered. Current status: {f.Status}.");

            f.Status         = "Delivered";
            f.DeliveryDate   = DateTime.UtcNow;
            f.ReceivedByName = receivedByName;
            f.UpdatedAt      = DateTime.UtcNow;

            _uow.Repository<OrderFulfillment>().Update(f);

            var order = await _uow.Repository<SalesOrder>().GetByIdAsync(f.SalesOrderId, ct);
            if (order != null)
            {
                order.Status    = "Delivered";
                order.UpdatedAt = DateTime.UtcNow;
                _uow.Repository<SalesOrder>().Update(order);
            }

            await _uow.SaveChangesAsync(ct);
            return f;
        }

        /// <summary>Marks the delivery as failed with a reason.</summary>
        public async Task<OrderFulfillment> MarkDeliveryFailedAsync(int fulfillmentId, string reason, int userId, CancellationToken ct = default)
        {
            var f = await _uow.Repository<OrderFulfillment>().GetByIdAsync(fulfillmentId, ct)
                ?? throw new InvalidOperationException($"Fulfilment {fulfillmentId} not found.");

            f.Status        = "Failed";
            f.FailureReason = reason;
            f.UpdatedAt     = DateTime.UtcNow;

            _uow.Repository<OrderFulfillment>().Update(f);
            await _uow.SaveChangesAsync(ct);
            return f;
        }

        /// <summary>Returns the fulfilment record for a specific sales order.</summary>
        public async Task<OrderFulfillment?> GetFulfillmentByOrderAsync(int salesOrderId, CancellationToken ct = default)
            => await _fulfillmentRepo.GetBySalesOrderAsync(salesOrderId, ct);

        /// <summary>Returns all fulfilments that are pending shipment.</summary>
        public async Task<IEnumerable<OrderFulfillment>> GetPendingFulfillmentsAsync(CancellationToken ct = default)
            => await _fulfillmentRepo.GetPendingShipmentAsync(ct);

        /// <summary>Generates a unique packing slip number in the format PS-{yyyyMMdd}-{seq}.</summary>
        public Task<string> GeneratePackingSlipNumberAsync(CancellationToken ct = default)
        {
            int seq;
            lock (_seqLock)
            {
                seq = ++_slipSequence;
            }
            return Task.FromResult($"PS-{DateTime.UtcNow:yyyyMMdd}-{seq:D4}");
        }

        /// <summary>Returns shipment and delivery metrics within a date range.</summary>
        public async Task<(int TotalShipments, int OnTimeDeliveries, decimal OnTimePercent, int FailedDeliveries)> GetFulfillmentMetricsAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var all = (await _fulfillmentRepo.FindAsync(
                f => f.ShippedDate.HasValue && f.ShippedDate.Value >= from && f.ShippedDate.Value <= to, ct)).ToList();

            var total   = all.Count;
            var onTime  = all.Count(f => f.Status == "Delivered" && f.DeliveryDate.HasValue);
            var failed  = all.Count(f => f.Status == "Failed");
            var pct     = total > 0 ? (decimal)onTime / total * 100 : 0;

            return (total, onTime, pct, failed);
        }
    }
}
