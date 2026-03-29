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
    /// Manages sub-contractor onboarding, challan creation/tracking, receiving with QC gate,
    /// payment processing, and performance scorecards.
    /// </summary>
    public class SubContractorService
    {
        private readonly IUnitOfWork _uow;
        private readonly SubContractorRepository _scRepo;
        private readonly ChallanRepository _challanRepo;
        private readonly ILogger<SubContractorService> _logger;

        private static readonly HashSet<(string From, string To)> ValidTransitions = new()
        {
            ("Draft",              "Sent"),
            ("Sent",               "Received"),
            ("Received",           "Accepted"),
            ("Received",           "Rejected"),
            ("Received",           "PartiallyAccepted"),
            ("Accepted",           "Paid"),
            ("PartiallyAccepted",  "Paid"),
            ("Draft",              "Cancelled"),
            ("Sent",               "Cancelled"),
        };

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public SubContractorService(
            IUnitOfWork uow,
            SubContractorRepository scRepo,
            ChallanRepository challanRepo,
            ILogger<SubContractorService> logger)
        {
            _uow = uow;
            _scRepo = scRepo;
            _challanRepo = challanRepo;
            _logger = logger;
        }

        /// <summary>Onboards a new sub-contractor.</summary>
        public async Task<SubContractor> OnboardSubContractorAsync(
            string name, string? mobile, string? email, string? address,
            string? skills, string? paymentTerms, string? gstNo,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Sub-contractor name is required.");

            var sc = new SubContractor
            {
                Name             = name,
                Mobile           = mobile,
                Email            = email,
                Address          = address,
                Skills           = skills,
                PaymentTerms     = paymentTerms,
                GSTNo            = gstNo,
                PerformanceScore = 3,
                IsActive         = true,
                CreatedAt        = DateTime.UtcNow,
                UpdatedAt        = DateTime.UtcNow
            };

            await _uow.Repository<SubContractor>().AddAsync(sc, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Sub-contractor '{Name}' onboarded (Id={Id})", name, sc.Id);
            return sc;
        }

        /// <summary>Creates a challan (outward delivery record) to a sub-contractor.</summary>
        public async Task<Challan> CreateChallanAsync(
            int subContractorId,
            DateTime? dueDate,
            int createdByUserId,
            IEnumerable<(string ItemDescription, int? FinishedGoodId, decimal Qty, string Unit, decimal Rate)> lines,
            string? notes = null,
            CancellationToken ct = default)
        {
            var lineList = lines.ToList();
            if (!lineList.Any())
                throw new InvalidOperationException("A challan must have at least one line item.");

            var challanNo = $"CH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            decimal total = 0;

            var challan = new Challan
            {
                ChallanNo       = challanNo,
                SubContractorId = subContractorId,
                Status          = "Draft",
                ChallanDate     = DateTime.UtcNow,
                DueDate         = dueDate,
                Notes           = notes,
                CreatedByUserId = createdByUserId,
                CreatedAt       = DateTime.UtcNow,
                UpdatedAt       = DateTime.UtcNow
            };

            foreach (var (desc, fgId, qty, unit, rate) in lineList)
            {
                var amount = qty * rate;
                total += amount;
                challan.Lines.Add(new ChallanLine
                {
                    ItemDescription = desc,
                    FinishedGoodId  = fgId,
                    Quantity        = qty,
                    Unit            = unit,
                    Rate            = rate,
                    Amount          = amount
                });
            }

            challan.TotalAmount = total;

            await _uow.Repository<Challan>().AddAsync(challan, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Challan {ChallanNo} created (Id={Id})", challanNo, challan.Id);
            return challan;
        }

        /// <summary>Advances challan status through the workflow state machine.</summary>
        public async Task<Challan> AdvanceChallanStatusAsync(int challanId, string toStatus, CancellationToken ct = default)
        {
            var challan = await _challanRepo.GetByIdAsync(challanId, ct)
                ?? throw new InvalidOperationException($"Challan {challanId} not found.");

            if (!ValidTransitions.Contains((challan.Status, toStatus)))
                throw new InvalidOperationException($"Invalid transition from '{challan.Status}' to '{toStatus}'.");

            challan.Status    = toStatus;
            challan.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<Challan>().Update(challan);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Challan {ChallanNo} status → '{Status}'", challan.ChallanNo, toStatus);
            return challan;
        }

        /// <summary>Records goods received back from a sub-contractor with QC gate.</summary>
        public async Task<ChallanReceival> ReceiveGoodsAsync(
            int challanId, decimal receivedQty, decimal acceptedQty, decimal rejectedQty,
            string qcStatus, string? qcRemarks, int receivedByUserId,
            CancellationToken ct = default)
        {
            var validStatuses = new[] { "Accepted", "Rejected", "PartiallyAccepted", "Pending" };
            if (!validStatuses.Contains(qcStatus))
                throw new InvalidOperationException($"Invalid QC status '{qcStatus}'.");

            var challan = await _challanRepo.GetByIdAsync(challanId, ct)
                ?? throw new InvalidOperationException($"Challan {challanId} not found.");

            if (challan.Status != "Sent")
                throw new InvalidOperationException("Can only receive goods for challans with 'Sent' status.");

            var receival = new ChallanReceival
            {
                ChallanId        = challanId,
                ReceivalDate     = DateTime.UtcNow,
                QCStatus         = qcStatus,
                ReceivedQuantity = receivedQty,
                AcceptedQuantity = acceptedQty,
                RejectedQuantity = rejectedQty,
                QCRemarks        = qcRemarks,
                ReceivedByUserId = receivedByUserId,
                CreatedAt        = DateTime.UtcNow
            };

            await _uow.Repository<ChallanReceival>().AddAsync(receival, ct);

            // Advance challan to Received
            challan.Status    = "Received";
            challan.UpdatedAt = DateTime.UtcNow;
            _uow.Repository<Challan>().Update(challan);

            await _uow.SaveChangesAsync(ct);
            return receival;
        }

        /// <summary>
        /// Records a payment against a challan.
        /// Payment is only allowed when the challan is Accepted or PartiallyAccepted.
        /// </summary>
        public async Task<ChallanPayment> ProcessPaymentAsync(
            int challanId, decimal amount, string paymentMode, string? referenceNo,
            string? notes, int processedByUserId, CancellationToken ct = default)
        {
            var challan = await _challanRepo.GetWithDetailsAsync(challanId, ct)
                ?? throw new InvalidOperationException($"Challan {challanId} not found.");

            if (challan.Status != "Accepted" && challan.Status != "PartiallyAccepted")
                throw new InvalidOperationException("Payment can only be processed for Accepted or PartiallyAccepted challans.");

            var totalPaid = challan.Payments.Sum(p => p.Amount);
            if (totalPaid + amount > challan.TotalAmount)
                throw new InvalidOperationException("Payment amount exceeds challan total.");

            var payment = new ChallanPayment
            {
                ChallanId         = challanId,
                PaymentDate       = DateTime.UtcNow,
                Amount            = amount,
                PaymentMode       = paymentMode,
                ReferenceNo       = referenceNo,
                Notes             = notes,
                ProcessedByUserId = processedByUserId
            };

            await _uow.Repository<ChallanPayment>().AddAsync(payment, ct);

            // Mark fully paid
            if (totalPaid + amount >= challan.TotalAmount)
            {
                challan.Status    = "Paid";
                challan.UpdatedAt = DateTime.UtcNow;
                _uow.Repository<Challan>().Update(challan);
            }

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Payment ₹{Amount} recorded for Challan {ChallanNo}", amount, challan.ChallanNo);
            return payment;
        }

        /// <summary>Returns overdue challans.</summary>
        public async Task<IEnumerable<Challan>> GetOverdueChallansAsync(CancellationToken ct = default)
            => await _challanRepo.GetOverdueAsync(ct);

        /// <summary>Returns top-performing sub-contractors.</summary>
        public async Task<IEnumerable<SubContractor>> GetTopPerformersAsync(int top = 10, CancellationToken ct = default)
            => await _scRepo.GetTopPerformersAsync(top, ct);
    }
}
