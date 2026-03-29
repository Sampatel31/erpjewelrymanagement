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
    /// Manages purchase order lifecycle from creation through receipt and invoice matching.
    /// </summary>
    public class PurchaseOrderService
    {
        private readonly IUnitOfWork _uow;
        private readonly PurchaseOrderRepository _poRepo;
        private readonly AuditTrailService _auditService;
        private readonly ILogger<PurchaseOrderService> _logger;

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public PurchaseOrderService(
            IUnitOfWork uow,
            PurchaseOrderRepository poRepo,
            AuditTrailService auditService,
            ILogger<PurchaseOrderService> logger)
        {
            _uow = uow;
            _poRepo = poRepo;
            _auditService = auditService;
            _logger = logger;
        }

        /// <summary>Creates a new purchase order with the supplied line items.</summary>
        public async Task<PurchaseOrder> CreatePOAsync(
            int supplierId,
            IEnumerable<(string Desc, int? ItemId, decimal Qty, decimal UnitPrice, decimal TaxPct)> lines,
            DateTime? expectedDeliveryDate,
            string? terms,
            int createdByUserId,
            CancellationToken ct = default)
        {
            var poNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var po = new PurchaseOrder
            {
                PONumber             = poNumber,
                SupplierId           = supplierId,
                OrderDate            = DateTime.UtcNow,
                ExpectedDeliveryDate = expectedDeliveryDate,
                Status               = "Draft",
                Terms                = terms,
                CreatedByUserId      = createdByUserId,
                CreatedAt            = DateTime.UtcNow,
                UpdatedAt            = DateTime.UtcNow
            };

            decimal total    = 0;
            decimal taxTotal = 0;
            int sort = 1;

            foreach (var (desc, itemId, qty, unitPrice, taxPct) in lines)
            {
                var lineTotal = (double)qty * (double)unitPrice;
                var lineTax   = lineTotal * (double)taxPct / 100;
                total    += (decimal)lineTotal;
                taxTotal += (decimal)lineTax;

                po.Lines.Add(new PurchaseOrderLine
                {
                    ItemDescription   = desc,
                    ItemId            = itemId,
                    Quantity          = qty,
                    UnitPrice         = unitPrice,
                    TaxPercent        = taxPct,
                    LineTotal         = (decimal)lineTotal,
                    ReceivedQuantity  = 0,
                    SortOrder         = sort++
                });
            }

            po.TotalAmount = total;
            po.TaxAmount   = taxTotal;
            po.NetAmount   = total + taxTotal;

            await _uow.Repository<PurchaseOrder>().AddAsync(po, ct);
            await _uow.SaveChangesAsync(ct);

            await _auditService.LogCreateAsync("PurchaseOrder", po.Id.ToString(), $"{{\"poNumber\":\"{po.PONumber}\"}}", createdByUserId, null, "Purchasing", ct);
            return po;
        }

        /// <summary>Sends a Draft PO to the supplier (Draft → Sent).</summary>
        public async Task<PurchaseOrder> SendPOAsync(int poId, int userId, CancellationToken ct = default)
            => await TransitionAsync(poId, "Draft", "Sent", userId, ct);

        /// <summary>Marks a Sent PO as confirmed by the supplier (Sent → Confirmed).</summary>
        public async Task<PurchaseOrder> ConfirmPOAsync(int poId, int userId, CancellationToken ct = default)
            => await TransitionAsync(poId, "Sent", "Confirmed", userId, ct);

        /// <summary>Records received quantities against PO lines. Updates status to Received or PartiallyReceived.</summary>
        public async Task<PurchaseOrder> ReceivePOAsync(int poId, int userId, IEnumerable<(int LineId, decimal ReceivedQty)> receivedLines, CancellationToken ct = default)
        {
            var po = await _poRepo.GetWithLinesAsync(poId, ct)
                ?? throw new InvalidOperationException($"Purchase order {poId} not found.");

            if (po.Status != "Confirmed" && po.Status != "PartiallyReceived")
                throw new InvalidOperationException($"Only Confirmed or PartiallyReceived orders can be received. Current status: {po.Status}.");

            var tracked = await _uow.Repository<PurchaseOrder>().GetByIdAsync(poId, ct)!;

            foreach (var (lineId, receivedQty) in receivedLines)
            {
                var line = po.Lines.FirstOrDefault(l => l.Id == lineId);
                if (line == null) continue;

                var trackedLine = await _uow.Repository<PurchaseOrderLine>().GetByIdAsync(lineId, ct)!;
                trackedLine!.ReceivedQuantity = receivedQty;
                _uow.Repository<PurchaseOrderLine>().Update(trackedLine);
            }

            // Refresh lines from context to check fulfilment
            var allLines = po.Lines.ToList();
            bool fullyReceived = allLines.All(l =>
            {
                var updated = receivedLines.FirstOrDefault(r => r.LineId == l.Id);
                var received = updated.LineId != 0 ? updated.ReceivedQty : l.ReceivedQuantity;
                return received >= l.Quantity;
            });

            tracked!.Status    = fullyReceived ? "Received" : "PartiallyReceived";
            tracked.UpdatedAt  = DateTime.UtcNow;
            _uow.Repository<PurchaseOrder>().Update(tracked);

            await _uow.SaveChangesAsync(ct);
            return tracked;
        }

        /// <summary>Marks a Confirmed or Received PO as Invoiced.</summary>
        public async Task<PurchaseOrder> InvoicePOAsync(int poId, int userId, CancellationToken ct = default)
        {
            var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(poId, ct)
                ?? throw new InvalidOperationException($"Purchase order {poId} not found.");

            if (po.Status != "Confirmed" && po.Status != "Received" && po.Status != "PartiallyReceived")
                throw new InvalidOperationException($"Cannot invoice a PO in status {po.Status}.");

            return await TransitionAsync(poId, po.Status, "Invoiced", userId, ct);
        }

        /// <summary>Cancels a Draft or Sent PO.</summary>
        public async Task<PurchaseOrder> CancelPOAsync(int poId, int userId, CancellationToken ct = default)
        {
            var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(poId, ct)
                ?? throw new InvalidOperationException($"Purchase order {poId} not found.");

            if (po.Status != "Draft" && po.Status != "Sent")
                throw new InvalidOperationException($"Only Draft or Sent purchase orders can be cancelled. Current status: {po.Status}.");

            return await TransitionAsync(poId, po.Status, "Cancelled", userId, ct);
        }

        /// <summary>Returns all POs for a supplier.</summary>
        public async Task<IEnumerable<PurchaseOrder>> GetPOsBySupplierAsync(int supplierId, CancellationToken ct = default)
            => await _poRepo.GetBySupplierAsync(supplierId, ct);

        /// <summary>Returns all POs with a given status.</summary>
        public async Task<IEnumerable<PurchaseOrder>> GetPOsByStatusAsync(string status, CancellationToken ct = default)
            => await _poRepo.GetByStatusAsync(status, ct);

        /// <summary>Returns a PO with its lines included.</summary>
        public async Task<PurchaseOrder?> GetPOWithDetailsAsync(int poId, CancellationToken ct = default)
            => await _poRepo.GetWithLinesAsync(poId, ct);

        /// <summary>Returns POs that are overdue (past expected delivery date and not yet received).</summary>
        public async Task<IEnumerable<PurchaseOrder>> GetOverduePOsAsync(CancellationToken ct = default)
            => await _poRepo.GetOverdueAsync(ct);

        /// <summary>Returns performance metrics for a supplier within a date range.</summary>
        public async Task<(int TotalPOs, int OnTimePOs, decimal OnTimePercent, decimal TotalValue)> GetSupplierPerformanceAsync(int supplierId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var pos = (await _poRepo.GetByDateRangeAsync(from, to, ct))
                .Where(p => p.SupplierId == supplierId)
                .ToList();

            var total      = pos.Count;
            var onTime     = pos.Count(p => p.Status == "Received" && p.ExpectedDeliveryDate.HasValue && DateTime.UtcNow <= p.ExpectedDeliveryDate.Value);
            var onTimePct  = total > 0 ? (decimal)onTime / total * 100 : 0;
            var totalValue = pos.Sum(p => p.NetAmount);

            return (total, onTime, onTimePct, totalValue);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private async Task<PurchaseOrder> TransitionAsync(int poId, string fromStatus, string toStatus, int userId, CancellationToken ct)
        {
            var po = await _uow.Repository<PurchaseOrder>().GetByIdAsync(poId, ct)
                ?? throw new InvalidOperationException($"Purchase order {poId} not found.");

            if (po.Status != fromStatus)
                throw new InvalidOperationException($"Cannot transition from '{po.Status}' to '{toStatus}'. Expected current status: '{fromStatus}'.");

            po.Status    = toStatus;
            po.UpdatedAt = DateTime.UtcNow;

            _uow.Repository<PurchaseOrder>().Update(po);
            await _uow.SaveChangesAsync(ct);
            await _auditService.LogUpdateAsync("PurchaseOrder", poId.ToString(), fromStatus, toStatus, userId, null, "Purchasing", ct);
            return po;
        }
    }
}
