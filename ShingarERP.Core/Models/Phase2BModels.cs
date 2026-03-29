using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShingarERP.Core.Models
{
    // ─────────────────────────────────────────────────────────────
    // Phase 2B – Customer Lifecycle, Advanced Accounting, Orders, Reporting
    // ─────────────────────────────────────────────────────────────

    // ── Customer Lifecycle ────────────────────────────────────────

    /// <summary>KYC (Know Your Customer) verification record for compliance.</summary>
    public class CustomerKYC
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }

        /// <summary>KYC status: Pending/InProgress/Verified/Expired/Rejected</summary>
        [Required, MaxLength(20)]
        public string KYCStatus { get; set; } = "Pending";

        /// <summary>Hashed Aadhaar number for secure storage.</summary>
        [MaxLength(100)]
        public string? AadhaarHash { get; set; }

        public bool PANVerified { get; set; } = false;

        [MaxLength(50)]
        public string? AddressProofType { get; set; }

        [MaxLength(100)]
        public string? AddressProofNumber { get; set; }

        [MaxLength(50)]
        public string? IncomeProofType { get; set; }

        [MaxLength(100)]
        public string? Occupation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AnnualIncome { get; set; }

        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;
        public DateTime? VerifiedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public int? VerifiedByUserId { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer Customer { get; set; } = null!;
    }

    /// <summary>Credit limit and utilization tracking per customer.</summary>
    public class CustomerCreditLimit
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditLimit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UtilizedAmount { get; set; }

        /// <summary>Available credit = CreditLimit - UtilizedAmount.</summary>
        [NotMapped]
        public decimal AvailableCredit => CreditLimit - UtilizedAmount;

        /// <summary>Utilization percentage (0-100).</summary>
        [NotMapped]
        public decimal UtilizationPercent => CreditLimit > 0 ? UtilizedAmount / CreditLimit * 100 : 0;

        public DateTime LastReviewDate { get; set; } = DateTime.UtcNow;
        public DateTime NextReviewDate { get; set; } = DateTime.UtcNow.AddMonths(6);

        public int? ReviewedByUserId { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer Customer { get; set; } = null!;
    }

    /// <summary>Recorded customer preferences to aid personalized sales.</summary>
    public class CustomerPreference
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }

        [MaxLength(30)]
        public string? MetalType { get; set; }

        [MaxLength(50)]
        public string? DesignStyle { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinPriceRange { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxPriceRange { get; set; }

        [MaxLength(100)]
        public string? PreferredOccasion { get; set; }

        [MaxLength(50)]
        public string? SizePreference { get; set; }

        [MaxLength(30)]
        public string? ColorPreference { get; set; }

        [MaxLength(500)]
        public string? NotesFromStaff { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
        public int? UpdatedByUserId { get; set; }

        // Navigation
        public Customer Customer { get; set; } = null!;
    }

    /// <summary>Loyalty programme enrollment and tier status per customer.</summary>
    public class LoyaltyProgram
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }

        public int CurrentPoints { get; set; }
        public int LifetimePoints { get; set; }

        /// <summary>Current tier: Bronze / Silver / Gold / Platinum</summary>
        [Required, MaxLength(20)]
        public string CurrentTier { get; set; } = "Bronze";

        public DateTime? TierUpgradeDate { get; set; }
        public DateTime? TierDowngradeDate { get; set; }
        public DateTime? LastPointsEarnedDate { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer Customer { get; set; } = null!;
        public ICollection<LoyaltyTransaction> Transactions { get; set; } = new List<LoyaltyTransaction>();
    }

    /// <summary>Individual point earn/redeem/expire/adjust transaction on a loyalty programme.</summary>
    public class LoyaltyTransaction
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(LoyaltyProgram))]
        public int LoyaltyProgramId { get; set; }

        /// <summary>Transaction type: Earn / Redeem / Expire / Adjust</summary>
        [Required, MaxLength(20)]
        public string TransactionType { get; set; } = string.Empty;

        public int Points { get; set; }
        public int BalanceAfter { get; set; }

        [MaxLength(30)]
        public string? ReferenceType { get; set; }

        public int? ReferenceId { get; set; }

        [MaxLength(300)]
        public string? Description { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public LoyaltyProgram LoyaltyProgram { get; set; } = null!;
    }

    /// <summary>Interaction log between staff and a customer (calls, visits, emails, etc.).</summary>
    public class CustomerInteraction
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }

        /// <summary>Type: Call / Visit / Email / SMS / WhatsApp / Purchase / Inquiry</summary>
        [Required, MaxLength(30)]
        public string InteractionType { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime InteractionDate { get; set; } = DateTime.UtcNow;
        public int? DurationMinutes { get; set; }

        /// <summary>Outcome: Purchased / Interested / FollowupRequired / NoAction</summary>
        [MaxLength(50)]
        public string? OutcomeType { get; set; }

        public DateTime? FollowupDate { get; set; }
        public int? StaffUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer Customer { get; set; } = null!;
    }

    // ── Accounting Extensions ─────────────────────────────────────

    /// <summary>General Ledger entry providing a running balance per account.</summary>
    public class GeneralLedger
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Account))]
        public int AccountId { get; set; }

        public DateTime PostingDate { get; set; }

        [Required, MaxLength(30)]
        public string VoucherNo { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string VoucherType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal DebitAmount { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal CreditAmount { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal RunningBalance { get; set; }

        [MaxLength(500)]
        public string? Narration { get; set; }

        public int? CostCenterId { get; set; }

        public bool IsReconciled { get; set; } = false;
        public DateTime? ReconcileDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? JournalEntryLineId { get; set; }

        // Navigation
        public Account Account { get; set; } = null!;
    }

    /// <summary>Snapshot trial balance for a given reporting period.</summary>
    public class TrialBalance
    {
        [Key]
        public int Id { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Account))]
        public int AccountId { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal OpeningDebit { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal OpeningCredit { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal PeriodDebit { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal PeriodCredit { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ClosingDebit { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ClosingCredit { get; set; }

        public int? GeneratedByUserId { get; set; }

        [MaxLength(300)]
        public string? Remarks { get; set; }

        // Navigation
        public Account Account { get; set; } = null!;
    }

    /// <summary>Template definition for financial statement generation.</summary>
    public class FinancialStatementTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>Statement type: BalanceSheet / ProfitLoss / CashFlow</summary>
        [Required, MaxLength(20)]
        public string StatementType { get; set; } = string.Empty;

        /// <summary>JSON structure defining sections and account groupings.</summary>
        [Required, MaxLength(4000)]
        public string Configuration { get; set; } = string.Empty;

        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Generated financial statement with computed totals stored as JSON.</summary>
    public class FinancialStatement
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Statement type: BalanceSheet / ProfitLoss / CashFlow</summary>
        [Required, MaxLength(20)]
        public string StatementType { get; set; } = string.Empty;

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;

        public int? TemplateId { get; set; }

        /// <summary>Full statement data serialised as JSON.</summary>
        [MaxLength(8000)]
        public string StatementData { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAssets { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalLiabilities { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? NetProfit { get; set; }

        public int? GeneratedByUserId { get; set; }
        public bool IsApproved { get; set; } = false;
        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedDate { get; set; }

        // Navigation
        public FinancialStatementTemplate? Template { get; set; }
    }

    /// <summary>Cost centre for management accounting and budget allocation.</summary>
    public class CostCenter
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(20)]
        public string CostCenterCode { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string CostCenterName { get; set; } = string.Empty;

        /// <summary>Type: Department / Project / Location / Activity</summary>
        [Required, MaxLength(30)]
        public string CostCenterType { get; set; } = string.Empty;

        public int? ParentCostCenterId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BudgetAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualAmount { get; set; }

        /// <summary>Budget variance = BudgetAmount - ActualAmount.</summary>
        [NotMapped]
        public decimal Variance => BudgetAmount - ActualAmount;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public CostCenter? ParentCostCenter { get; set; }
        public ICollection<CostCenter> ChildCostCenters { get; set; } = new List<CostCenter>();
    }

    /// <summary>Budget allocation per account per cost centre per fiscal period.</summary>
    public class BudgetAllocation
    {
        [Key]
        public int Id { get; set; }

        public int? CostCenterId { get; set; }

        [ForeignKey(nameof(Account))]
        public int AccountId { get; set; }

        public int FiscalYear { get; set; }

        /// <summary>Period month: 1–12.</summary>
        public int PeriodMonth { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BudgetedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualAmount { get; set; }

        /// <summary>Variance = BudgetedAmount - ActualAmount.</summary>
        [NotMapped]
        public decimal Variance => BudgetedAmount - ActualAmount;

        /// <summary>Variance as percentage of budget.</summary>
        [NotMapped]
        public decimal VariancePercent => BudgetedAmount != 0
            ? (ActualAmount - BudgetedAmount) / BudgetedAmount * 100
            : 0;

        public bool IsApproved { get; set; } = false;
        public int? ApprovedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public CostCenter? CostCenter { get; set; }
        public Account Account { get; set; } = null!;
    }

    /// <summary>Immutable audit log for compliance (SOX, FEMA, GST regulations).</summary>
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string EntityName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string EntityId { get; set; } = string.Empty;

        /// <summary>Operation: Create / Update / Delete / Read</summary>
        [Required, MaxLength(20)]
        public string OperationType { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? OldValues { get; set; }

        [MaxLength(4000)]
        public string? NewValues { get; set; }

        [MaxLength(1000)]
        public string? ChangedFields { get; set; }

        public int? UserId { get; set; }

        [MaxLength(100)]
        public string? UserName { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(300)]
        public string? UserAgent { get; set; }

        [MaxLength(50)]
        public string? CorrelationId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string? Module { get; set; }

        [MaxLength(300)]
        public string? Remarks { get; set; }
    }

    /// <summary>Tax calculation record for GST/TDS/TCS per transaction.</summary>
    public class TaxCalculation
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Reference type: SalesOrder / PurchaseOrder / Invoice</summary>
        [Required, MaxLength(30)]
        public string ReferenceType { get; set; } = string.Empty;

        public int ReferenceId { get; set; }

        /// <summary>Tax type: GST / IGST / CGST / SGST / TDS / TCS</summary>
        [Required, MaxLength(20)]
        public string TaxType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxableAmount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CGSTAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SGSTAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal IGSTAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TDSAmount { get; set; }

        [MaxLength(10)]
        public string? HSNCode { get; set; }

        [MaxLength(20)]
        public string? TaxPeriod { get; set; }

        public bool IsFiledOnGST { get; set; } = false;
        public DateTime? FiledDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ── Orders Module ─────────────────────────────────────────────

    /// <summary>Sales order header for customer jewellery purchases.</summary>
    public class SalesOrder
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string OrderNo { get; set; } = string.Empty;

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public DateTime? RequiredDate { get; set; }

        /// <summary>Status: Draft/Submitted/Approved/InProduction/ReadyToShip/Shipped/Delivered/Cancelled/Returned</summary>
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Draft";

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [MaxLength(500)]
        public string? ShippingAddress { get; set; }

        public int? AssignedToUserId { get; set; }
        public int CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Customer Customer { get; set; } = null!;
        public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
        public ICollection<SalesOrderApproval> Approvals { get; set; } = new List<SalesOrderApproval>();
        public ICollection<OrderPaymentSchedule> PaymentSchedules { get; set; } = new List<OrderPaymentSchedule>();
    }

    /// <summary>Individual line item on a sales order.</summary>
    public class SalesOrderLine
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(SalesOrder))]
        public int SalesOrderId { get; set; }

        [ForeignKey(nameof(FinishedGood))]
        public int ItemId { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal DiscountPercent { get; set; }

        /// <summary>Line total = Quantity × UnitPrice × (1 - DiscountPercent/100).</summary>
        [NotMapped]
        public decimal LineTotal => Quantity * UnitPrice * (1 - DiscountPercent / 100);

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxPercent { get; set; }

        [MaxLength(300)]
        public string? Remarks { get; set; }

        public int SortOrder { get; set; }

        // Navigation
        public SalesOrder SalesOrder { get; set; } = null!;
        public FinishedGood FinishedGood { get; set; } = null!;
    }

    /// <summary>Approval step in the sales order approval workflow.</summary>
    public class SalesOrderApproval
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(SalesOrder))]
        public int SalesOrderId { get; set; }

        /// <summary>Approval level: 1, 2, or 3.</summary>
        public int ApprovalLevel { get; set; }

        public int ApproverUserId { get; set; }

        /// <summary>Status: Pending / Approved / Rejected</summary>
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime? ApprovalDate { get; set; }

        [MaxLength(500)]
        public string? Comments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public SalesOrder SalesOrder { get; set; } = null!;
    }

    /// <summary>Purchase order sent to a supplier.</summary>
    public class PurchaseOrder
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string PONumber { get; set; } = string.Empty;

        [ForeignKey(nameof(Supplier))]
        public int SupplierId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public DateTime? ExpectedDeliveryDate { get; set; }

        /// <summary>Status: Draft/Sent/Confirmed/PartiallyReceived/Received/Invoiced/Cancelled</summary>
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Draft";

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        [MaxLength(500)]
        public string? Terms { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public int CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Supplier Supplier { get; set; } = null!;
        public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    }

    /// <summary>Individual line item on a purchase order.</summary>
    public class PurchaseOrderLine
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(PurchaseOrder))]
        public int PurchaseOrderId { get; set; }

        [Required, MaxLength(200)]
        public string ItemDescription { get; set; } = string.Empty;

        public int? ItemId { get; set; }

        [Column(TypeName = "decimal(12,4)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxPercent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [Column(TypeName = "decimal(12,4)")]
        public decimal ReceivedQuantity { get; set; }

        [MaxLength(300)]
        public string? Remarks { get; set; }

        public int SortOrder { get; set; }

        // Navigation
        public PurchaseOrder PurchaseOrder { get; set; } = null!;
        public FinishedGood? FinishedGood { get; set; }
    }

    /// <summary>Fulfilment record tracking packing and shipment of a sales order.</summary>
    public class OrderFulfillment
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(SalesOrder))]
        public int SalesOrderId { get; set; }

        [MaxLength(30)]
        public string? PackingSlipNo { get; set; }

        public DateTime? PackingDate { get; set; }

        [MaxLength(100)]
        public string? ShipmentTrackingNo { get; set; }

        public DateTime? ShippedDate { get; set; }
        public DateTime? DeliveryDate { get; set; }

        [MaxLength(100)]
        public string? ShippingProvider { get; set; }

        /// <summary>Status: Pending/Packing/Packed/Shipped/OutForDelivery/Delivered/Failed</summary>
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [MaxLength(300)]
        public string? FailureReason { get; set; }

        [MaxLength(100)]
        public string? ReceivedByName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public SalesOrder SalesOrder { get; set; } = null!;
    }

    /// <summary>Return request raised against a delivered sales order.</summary>
    public class OrderReturn
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string ReturnNo { get; set; } = string.Empty;

        [ForeignKey(nameof(SalesOrder))]
        public int SalesOrderId { get; set; }

        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

        /// <summary>Reason code: QualityIssue / WrongItem / CustomerChanged / Damaged / Other</summary>
        [Required, MaxLength(50)]
        public string ReasonCode { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? ReasonDescription { get; set; }

        /// <summary>Status: Pending / Approved / Received / Processed / Rejected</summary>
        [Required, MaxLength(20)]
        public string ReturnStatus { get; set; } = "Pending";

        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; }

        [MaxLength(30)]
        public string? RefundMethod { get; set; }

        public DateTime? RefundDate { get; set; }
        public int? ProcessedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public SalesOrder SalesOrder { get; set; } = null!;
    }

    /// <summary>Instalment payment schedule line for a sales order.</summary>
    public class OrderPaymentSchedule
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(SalesOrder))]
        public int SalesOrderId { get; set; }

        public DateTime DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DueAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        public DateTime? PaymentDate { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? ReferenceNo { get; set; }

        public bool IsPaid { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public SalesOrder SalesOrder { get; set; } = null!;
    }

    // ── Reporting ─────────────────────────────────────────────────

    /// <summary>Report template with query and column definitions.</summary>
    public class ReportTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string TemplateName { get; set; } = string.Empty;

        /// <summary>Report type: Sales / Purchase / Inventory / Customer / Financial / Custom</summary>
        [Required, MaxLength(50)]
        public string ReportType { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required, MaxLength(8000)]
        public string QueryDefinition { get; set; } = string.Empty;

        [Required, MaxLength(4000)]
        public string ColumnDefinitions { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string? FilterDefinitions { get; set; }

        public bool IsSystem { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Scheduled automatic report run configuration.</summary>
    public class ReportSchedule
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Template))]
        public int TemplateId { get; set; }

        [Required, MaxLength(100)]
        public string ScheduleName { get; set; } = string.Empty;

        /// <summary>Frequency: Daily / Weekly / Monthly / Quarterly / Yearly</summary>
        [Required, MaxLength(20)]
        public string Frequency { get; set; } = string.Empty;

        public DateTime NextRunDate { get; set; }
        public DateTime? LastRunDate { get; set; }

        /// <summary>Comma-separated email recipients.</summary>
        [Required, MaxLength(1000)]
        public string Recipients { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ReportTemplate Template { get; set; } = null!;
    }

    /// <summary>Dashboard widget configuration (chart, KPI card, table, gauge).</summary>
    public class DashboardWidget
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string WidgetName { get; set; } = string.Empty;

        /// <summary>Type: Chart / KPICard / Table / Gauge</summary>
        [Required, MaxLength(50)]
        public string WidgetType { get; set; } = string.Empty;

        /// <summary>JSON configuration for layout and visual settings.</summary>
        [Required, MaxLength(4000)]
        public string Configuration { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string DataSource { get; set; } = string.Empty;

        public int RefreshIntervalMinutes { get; set; } = 5;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Key Performance Indicator with current, target, and previous values.</summary>
    public class KPI
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string KPICode { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string KPIName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        [Required, MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal CurrentValue { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal TargetValue { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? PreviousValue { get; set; }

        [Required, MaxLength(20)]
        public string Unit { get; set; } = string.Empty;

        public bool IsIncreaseGood { get; set; } = true;

        /// <summary>Trend direction based on current vs previous value: Up / Down / Flat.</summary>
        [NotMapped]
        public string TrendDirection => CurrentValue > (PreviousValue ?? CurrentValue) ? "Up"
                                      : CurrentValue < (PreviousValue ?? CurrentValue) ? "Down"
                                      : "Flat";

        /// <summary>Achievement as percentage of target.</summary>
        [NotMapped]
        public decimal AchievementPercent => TargetValue != 0 ? CurrentValue / TargetValue * 100 : 0;

        public DateTime AsOfDate { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
