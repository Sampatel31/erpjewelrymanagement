using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShingarERP.Core.Models
{
    // ─────────────────────────────────────────────────────────────
    // Phase 2C – Manufacturing, Quality Control, Design, Sub-Contractor
    // ─────────────────────────────────────────────────────────────

    // ── Manufacturing Module (Module 03) ─────────────────────────

    /// <summary>Karigar (craftsman) master record.</summary>
    public class Karigar
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [MaxLength(20)] public string? Mobile { get; set; }
        [MaxLength(200)] public string? Address { get; set; }
        [MaxLength(50)] public string? EmployeeCode { get; set; }
        /// <summary>Experience in years.</summary>
        public int ExperienceYears { get; set; }
        /// <summary>Performance rating 1-5 (one decimal place, e.g. 3.5).</summary>
        [Column(TypeName = "decimal(4,2)")] public decimal PerformanceRating { get; set; } = 3m;
        /// <summary>Availability status: Available/Busy/OnLeave/Inactive</summary>
        [MaxLength(20)] public string AvailabilityStatus { get; set; } = "Available";
        [Column(TypeName = "decimal(14,2)")] public decimal DailyRate { get; set; }
        public DateTime JoiningDate { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<KarigarSkill> Skills { get; set; } = new List<KarigarSkill>();
        public ICollection<KarigarPerformance> Performances { get; set; } = new List<KarigarPerformance>();
        public ICollection<JobCard> JobCards { get; set; } = new List<JobCard>();
        public ICollection<JobCardLabor> LaborEntries { get; set; } = new List<JobCardLabor>();
    }

    /// <summary>Karigar skill matrix entry.</summary>
    public class KarigarSkill
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Karigar))] public int KarigarId { get; set; }
        [Required, MaxLength(100)] public string SkillName { get; set; } = string.Empty;
        /// <summary>Proficiency level 1–5.</summary>
        public int ProficiencyLevel { get; set; } = 1;
        [MaxLength(200)] public string? Notes { get; set; }
        public DateTime CertifiedOn { get; set; } = DateTime.UtcNow;
        public Karigar Karigar { get; set; } = null!;
    }

    /// <summary>Monthly performance snapshot for a karigar.</summary>
    public class KarigarPerformance
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Karigar))] public int KarigarId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int ItemsProduced { get; set; }
        [Column(TypeName = "decimal(5,2)")] public decimal QualityScore { get; set; }
        [Column(TypeName = "decimal(5,2)")] public decimal OnTimePercent { get; set; }
        [Column(TypeName = "decimal(5,2)")] public decimal WastePercent { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalLaborCost { get; set; }
        [MaxLength(500)] public string? Remarks { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
        public Karigar Karigar { get; set; } = null!;
    }

    /// <summary>Manufacturing job card header – links a design to production.</summary>
    public class JobCard
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(30)] public string JobCardNo { get; set; } = string.Empty;
        public int? SalesOrderId { get; set; }
        public int? DesignId { get; set; }
        [ForeignKey(nameof(Karigar))] public int? KarigarId { get; set; }
        [MaxLength(50)] public string Status { get; set; } = "Draft";
        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal EstimatedHours { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal ActualHours { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal EstimatedCost { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal ActualCost { get; set; }
        [MaxLength(500)] public string? Instructions { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Karigar? Karigar { get; set; }
        public Design? Design { get; set; }
        public ICollection<JobCardStage> Stages { get; set; } = new List<JobCardStage>();
        public ICollection<JobCardHistory> History { get; set; } = new List<JobCardHistory>();
        public ICollection<JobCardLabor> Labor { get; set; } = new List<JobCardLabor>();
        public ICollection<JobCardMaterial> Materials { get; set; } = new List<JobCardMaterial>();
    }

    /// <summary>Individual stage within a job card workflow.</summary>
    public class JobCardStage
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(JobCard))] public int JobCardId { get; set; }
        [Required, MaxLength(50)] public string StageName { get; set; } = string.Empty;
        public int StageOrder { get; set; }
        [MaxLength(20)] public string Status { get; set; } = "Pending";
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? AssignedKarigarId { get; set; }
        [MaxLength(500)] public string? Remarks { get; set; }
        public JobCard JobCard { get; set; } = null!;
    }

    /// <summary>Audit trail entry for job card status changes.</summary>
    public class JobCardHistory
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(JobCard))] public int JobCardId { get; set; }
        [MaxLength(50)] public string FromStatus { get; set; } = string.Empty;
        [MaxLength(50)] public string ToStatus { get; set; } = string.Empty;
        public int ChangedByUserId { get; set; }
        [MaxLength(500)] public string? Reason { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public JobCard JobCard { get; set; } = null!;
    }

    /// <summary>Labor cost allocated per karigar per job card.</summary>
    public class JobCardLabor
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(JobCard))] public int JobCardId { get; set; }
        [ForeignKey(nameof(Karigar))] public int KarigarId { get; set; }
        [Column(TypeName = "decimal(10,2)")] public decimal HoursWorked { get; set; }
        [Column(TypeName = "decimal(14,4)")] public decimal RatePerHour { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal LaborCost { get; set; }
        public DateTime WorkDate { get; set; } = DateTime.UtcNow;
        [MaxLength(200)] public string? Notes { get; set; }
        public JobCard JobCard { get; set; } = null!;
        public Karigar Karigar { get; set; } = null!;
    }

    /// <summary>Material consumed per job card (metal, stones, etc.).</summary>
    public class JobCardMaterial
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(JobCard))] public int JobCardId { get; set; }
        [Required, MaxLength(100)] public string MaterialType { get; set; } = string.Empty;
        [MaxLength(100)] public string? MaterialDescription { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal EstimatedQty { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal ActualQty { get; set; }
        [MaxLength(20)] public string Unit { get; set; } = "g";
        [Column(TypeName = "decimal(14,4)")] public decimal CostPerUnit { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal TotalCost { get; set; }
        public JobCard JobCard { get; set; } = null!;
    }

    // ── Melting & Alloy Module ─────────────────────────────────────

    /// <summary>Melting batch header for gold/metal processing.</summary>
    public class MeltingBatch
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(30)] public string BatchNo { get; set; } = string.Empty;
        [Required, MaxLength(50)] public string MetalType { get; set; } = string.Empty;
        [Column(TypeName = "decimal(12,4)")] public decimal GrossWeight { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal NetWeight { get; set; }
        [Column(TypeName = "decimal(7,4)")] public decimal MeltingLossPercent { get; set; }
        /// <summary>Batch status: Pending/Melting/Complete/Distributed</summary>
        [MaxLength(20)] public string Status { get; set; } = "Pending";
        public DateTime BatchDate { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        [MaxLength(500)] public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<MeltingInput> Inputs { get; set; } = new List<MeltingInput>();
        public ICollection<AlloyComposition> AlloyCompositions { get; set; } = new List<AlloyComposition>();
    }

    /// <summary>Source metal lot contributing to a melting batch.</summary>
    public class MeltingInput
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(MeltingBatch))] public int MeltingBatchId { get; set; }
        public int? MetalLotId { get; set; }
        [MaxLength(30)] public string? LotNumber { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal WeightUsed { get; set; }
        [MaxLength(200)] public string? Description { get; set; }
        public MeltingBatch MeltingBatch { get; set; } = null!;
    }

    /// <summary>Alloy composition recipe for a melting batch (proportions must sum to 100%).</summary>
    public class AlloyComposition
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(MeltingBatch))] public int MeltingBatchId { get; set; }
        [Required, MaxLength(50)] public string MetalType { get; set; } = string.Empty;
        [MaxLength(20)] public string? Purity { get; set; }
        [Column(TypeName = "decimal(7,4)")] public decimal Proportion { get; set; }
        public int Version { get; set; } = 1;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public MeltingBatch MeltingBatch { get; set; } = null!;
    }

    /// <summary>Version history for alloy composition recipe changes.</summary>
    public class AlloyCompositionHistory
    {
        [Key] public int Id { get; set; }
        public int MeltingBatchId { get; set; }
        [Required, MaxLength(50)] public string MetalType { get; set; } = string.Empty;
        [Column(TypeName = "decimal(7,4)")] public decimal OldProportion { get; set; }
        [Column(TypeName = "decimal(7,4)")] public decimal NewProportion { get; set; }
        public int ChangedByUserId { get; set; }
        [MaxLength(200)] public string? Reason { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }

    // ── Quality Control Module (Module 05) ──────────────────────

    /// <summary>QC inspection record for a finished item or job card.</summary>
    public class QCRecord
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(30)] public string QCNo { get; set; } = string.Empty;
        public int? JobCardId { get; set; }
        public int? FinishedGoodId { get; set; }
        public int? InspectedByKarigarId { get; set; }
        public int InspectorUserId { get; set; }
        /// <summary>Inspection result: Pass/Fail/Conditional</summary>
        [MaxLength(20)] public string Result { get; set; } = "Pending";
        [MaxLength(500)] public string? DefectNotes { get; set; }
        [Column(TypeName = "decimal(5,2)")] public decimal QualityScore { get; set; }
        public DateTime InspectionDate { get; set; } = DateTime.UtcNow;
        public DateTime? ReinspectionDate { get; set; }
        [MaxLength(500)] public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<QCDefect> Defects { get; set; } = new List<QCDefect>();
    }

    /// <summary>Defect recorded during a QC inspection.</summary>
    public class QCDefect
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(QCRecord))] public int QCRecordId { get; set; }
        [Required, MaxLength(20)] public string DefectCode { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;
        /// <summary>Severity: Critical/Major/Minor</summary>
        [MaxLength(20)] public string Severity { get; set; } = "Minor";
        [Column(TypeName = "decimal(12,2)")] public decimal RemedyCost { get; set; }
        public bool IsResolved { get; set; } = false;
        [MaxLength(200)] public string? Resolution { get; set; }
        public QCRecord QCRecord { get; set; } = null!;
    }

    /// <summary>Inspection checklist template tied to an item category.</summary>
    public class QCChecklist
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(100)] public string ChecklistName { get; set; } = string.Empty;
        public int? ItemCategoryId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<QCChecklistItem> Items { get; set; } = new List<QCChecklistItem>();
    }

    /// <summary>Individual check item within a QC checklist.</summary>
    public class QCChecklistItem
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(QCChecklist))] public int QCChecklistId { get; set; }
        [Required, MaxLength(200)] public string CheckName { get; set; } = string.Empty;
        [MaxLength(50)] public string? Tolerance { get; set; }
        public bool IsMandatory { get; set; } = true;
        public int SortOrder { get; set; }
        public QCChecklist QCChecklist { get; set; } = null!;
    }

    /// <summary>Catalog of rejection reason codes.</summary>
    public class RejectionReason
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(20)] public string ReasonCode { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;
        /// <summary>Severity: Critical/Major/Minor</summary>
        [MaxLength(20)] public string Severity { get; set; } = "Minor";
        public bool IsActive { get; set; } = true;
    }

    /// <summary>Assay lab test result for a metal lot or finished good.</summary>
    public class AssayTest
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(30)] public string TestNo { get; set; } = string.Empty;
        public int? MetalLotId { get; set; }
        public int? FinishedGoodId { get; set; }
        [Required, MaxLength(100)] public string LabName { get; set; } = string.Empty;
        [MaxLength(50)] public string? CertificateNo { get; set; }
        [Column(TypeName = "decimal(7,4)")] public decimal TestedPurity { get; set; }
        [Column(TypeName = "decimal(7,4)")] public decimal DeclaredPurity { get; set; }
        [Column(TypeName = "decimal(7,4)")] public decimal PurityVariance { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal TestedWeight { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal DeclaredWeight { get; set; }
        [Column(TypeName = "decimal(7,4)")] public decimal WeightVariance { get; set; }
        public DateTime TestDate { get; set; } = DateTime.UtcNow;
        [MaxLength(500)] public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ── Design Module (Module 06) ───────────────────────────────

    /// <summary>Design master for jewellery designs.</summary>
    public class Design
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(30)] public string DesignCode { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string DesignName { get; set; } = string.Empty;
        public int? ItemCategoryId { get; set; }
        [MaxLength(50)] public string? MetalType { get; set; }
        /// <summary>Complexity: Simple/Medium/Complex/Intricate</summary>
        [MaxLength(20)] public string Complexity { get; set; } = "Medium";
        [Column(TypeName = "decimal(10,2)")] public decimal EstimatedLaborHours { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal EstimatedMetalWeight { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal BasePrice { get; set; }
        /// <summary>Status: Draft/Review/Approved/Active/Archived</summary>
        [MaxLength(20)] public string Status { get; set; } = "Draft";
        public int? CollectionId { get; set; }
        [MaxLength(500)] public string? Description { get; set; }
        public int PopularityScore { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DesignCollection? Collection { get; set; }
        public ICollection<DesignPhoto> Photos { get; set; } = new List<DesignPhoto>();
        public ICollection<DesignBOM> BOMs { get; set; } = new List<DesignBOM>();
        public ICollection<DesignHistory> History { get; set; } = new List<DesignHistory>();
        public ICollection<DesignMakingCharge> MakingCharges { get; set; } = new List<DesignMakingCharge>();
        public ICollection<JobCard> JobCards { get; set; } = new List<JobCard>();
    }

    /// <summary>Seasonal or thematic design collection grouping.</summary>
    public class DesignCollection
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(100)] public string CollectionName { get; set; } = string.Empty;
        [MaxLength(20)] public string? Season { get; set; }
        public int? Year { get; set; }
        [MaxLength(500)] public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Design> Designs { get; set; } = new List<Design>();
    }

    /// <summary>Design photo asset (multi-angle).</summary>
    public class DesignPhoto
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Design))] public int DesignId { get; set; }
        /// <summary>View angle: Front/Back/Side/Detail</summary>
        [MaxLength(20)] public string ViewAngle { get; set; } = "Front";
        [Required, MaxLength(500)] public string PhotoUrl { get; set; } = string.Empty;
        [MaxLength(200)] public string? Caption { get; set; }
        public bool IsPrimary { get; set; } = false;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public Design Design { get; set; } = null!;
    }

    /// <summary>Making charge pricing template per design.</summary>
    public class DesignMakingCharge
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Design))] public int DesignId { get; set; }
        [Column(TypeName = "decimal(14,4)")] public decimal RatePerGram { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal FixedCharge { get; set; }
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; } = true;
        public Design Design { get; set; } = null!;
    }

    /// <summary>Bill of Materials for a design (stones, metals, etc.).</summary>
    public class DesignBOM
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Design))] public int DesignId { get; set; }
        [Required, MaxLength(100)] public string MaterialType { get; set; } = string.Empty;
        [MaxLength(200)] public string? MaterialDescription { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal EstimatedWeight { get; set; }
        [MaxLength(20)] public string Unit { get; set; } = "g";
        [Column(TypeName = "decimal(14,4)")] public decimal EstimatedCostPerUnit { get; set; }
        public Design Design { get; set; } = null!;
    }

    /// <summary>Version history record for design changes.</summary>
    public class DesignHistory
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Design))] public int DesignId { get; set; }
        public int Version { get; set; }
        [MaxLength(200)] public string? ChangeSummary { get; set; }
        [MaxLength(500)] public string? ChangeDetails { get; set; }
        public int ChangedByUserId { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public Design Design { get; set; } = null!;
    }

    // ── Sub-Contractor / Challan Module (Module 07) ─────────────

    /// <summary>Sub-contractor master record.</summary>
    public class SubContractor
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
        [MaxLength(20)] public string? Mobile { get; set; }
        [MaxLength(100)] public string? Email { get; set; }
        [MaxLength(300)] public string? Address { get; set; }
        [MaxLength(200)] public string? Skills { get; set; }
        [MaxLength(100)] public string? PaymentTerms { get; set; }
        [MaxLength(50)] public string? GSTNo { get; set; }
        [Column(TypeName = "decimal(5,2)")] public decimal PerformanceScore { get; set; } = 3m;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Challan> Challans { get; set; } = new List<Challan>();
    }

    /// <summary>Challan (outward delivery record) to a sub-contractor.</summary>
    public class Challan
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(30)] public string ChallanNo { get; set; } = string.Empty;
        [ForeignKey(nameof(SubContractor))] public int SubContractorId { get; set; }
        /// <summary>Status: Draft/Sent/Received/Accepted/Rejected/PartiallyAccepted/Paid</summary>
        [MaxLength(30)] public string Status { get; set; } = "Draft";
        public DateTime ChallanDate { get; set; } = DateTime.UtcNow;
        public DateTime? DueDate { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal TotalAmount { get; set; }
        [MaxLength(500)] public string? Notes { get; set; }
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public SubContractor SubContractor { get; set; } = null!;
        public ICollection<ChallanLine> Lines { get; set; } = new List<ChallanLine>();
        public ICollection<ChallanReceival> Receivals { get; set; } = new List<ChallanReceival>();
        public ICollection<ChallanPayment> Payments { get; set; } = new List<ChallanPayment>();
    }

    /// <summary>Individual line item in a challan.</summary>
    public class ChallanLine
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Challan))] public int ChallanId { get; set; }
        [Required, MaxLength(200)] public string ItemDescription { get; set; } = string.Empty;
        public int? FinishedGoodId { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal Quantity { get; set; }
        [MaxLength(20)] public string Unit { get; set; } = "pcs";
        [Column(TypeName = "decimal(14,4)")] public decimal Rate { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
        [MaxLength(200)] public string? Notes { get; set; }
        public Challan Challan { get; set; } = null!;
    }

    /// <summary>Record of goods received back from sub-contractor with QC result.</summary>
    public class ChallanReceival
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Challan))] public int ChallanId { get; set; }
        public DateTime ReceivalDate { get; set; } = DateTime.UtcNow;
        /// <summary>QC status: Accepted/Rejected/PartiallyAccepted</summary>
        [MaxLength(30)] public string QCStatus { get; set; } = "Pending";
        [Column(TypeName = "decimal(12,4)")] public decimal ReceivedQuantity { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal AcceptedQuantity { get; set; }
        [Column(TypeName = "decimal(12,4)")] public decimal RejectedQuantity { get; set; }
        [MaxLength(500)] public string? QCRemarks { get; set; }
        public int ReceivedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Challan Challan { get; set; } = null!;
    }

    /// <summary>Payment made to a sub-contractor against a challan.</summary>
    public class ChallanPayment
    {
        [Key] public int Id { get; set; }
        [ForeignKey(nameof(Challan))] public int ChallanId { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
        [MaxLength(50)] public string PaymentMode { get; set; } = "Cash";
        [MaxLength(100)] public string? ReferenceNo { get; set; }
        [MaxLength(200)] public string? Notes { get; set; }
        public int ProcessedByUserId { get; set; }
        public Challan Challan { get; set; } = null!;
    }
}
