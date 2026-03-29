using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShingarERP.Core.Models
{
    // ─────────────────────────────────────────────────────────────
    // Phase 2A – Advanced Multi-Location Inventory & Forecasting
    // ─────────────────────────────────────────────────────────────

    /// <summary>Physical location or store branch where inventory is held.</summary>
    public class InventoryLocation
    {
        [Key]
        public int LocationId { get; set; }

        [Required, MaxLength(50)]
        public string LocationCode { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LocationName { get; set; } = string.Empty;

        /// <summary>Type: Showroom / Warehouse / Workshop / Safe</summary>
        [MaxLength(30)]
        public string LocationType { get; set; } = "Showroom";

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(20)]
        public string? ContactPhone { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<LocationInventory> LocationInventories { get; set; } = new List<LocationInventory>();
        public ICollection<StockTransfer>     TransfersOut        { get; set; } = new List<StockTransfer>();
        public ICollection<StockTransfer>     TransfersIn         { get; set; } = new List<StockTransfer>();
        public ICollection<LocationCapacity>  Capacities          { get; set; } = new List<LocationCapacity>();
        public ICollection<ReorderPoint>      ReorderPoints       { get; set; } = new List<ReorderPoint>();
    }

    /// <summary>Snapshot of inventory quantity for one item at one location.</summary>
    public class LocationInventory
    {
        [Key]
        public int LocationInventoryId { get; set; }

        [ForeignKey(nameof(Location))]
        public int LocationId { get; set; }
        public InventoryLocation Location { get; set; } = null!;

        [ForeignKey(nameof(FinishedGood))]
        public int ItemId { get; set; }
        public FinishedGood FinishedGood { get; set; } = null!;

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(12,4)")]
        public decimal ReservedQuantity { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Stock transfer record between two locations (full audit trail).</summary>
    public class StockTransfer
    {
        [Key]
        public int TransferId { get; set; }

        [Required, MaxLength(30)]
        public string TransferNo { get; set; } = string.Empty;

        [ForeignKey(nameof(FromLocation))]
        public int FromLocationId { get; set; }
        public InventoryLocation FromLocation { get; set; } = null!;

        [ForeignKey(nameof(ToLocation))]
        public int ToLocationId { get; set; }
        public InventoryLocation ToLocation { get; set; } = null!;

        [ForeignKey(nameof(FinishedGood))]
        public int ItemId { get; set; }
        public FinishedGood FinishedGood { get; set; } = null!;

        public int Quantity { get; set; }

        /// <summary>Pending / InTransit / Completed / Cancelled</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public DateTime TransferDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        [MaxLength(100)]
        public string? InitiatedBy { get; set; }

        [MaxLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Historical sales data used as input for demand forecasting.</summary>
    public class ForecastData
    {
        [Key]
        public int ForecastDataId { get; set; }

        [ForeignKey(nameof(FinishedGood))]
        public int ItemId { get; set; }
        public FinishedGood FinishedGood { get; set; } = null!;

        [ForeignKey(nameof(Location))]
        public int? LocationId { get; set; }
        public InventoryLocation? Location { get; set; }

        /// <summary>Start of the period (week/month boundary).</summary>
        public DateTime PeriodStart { get; set; }

        /// <summary>End of the period.</summary>
        public DateTime PeriodEnd { get; set; }

        /// <summary>Actual quantity sold in the period.</summary>
        public int ActualQuantity { get; set; }

        /// <summary>Forecasted quantity for the period (filled after run).</summary>
        [Column(TypeName = "decimal(10,4)")]
        public decimal ForecastedQuantity { get; set; }

        /// <summary>Absolute percentage error for accuracy tracking.</summary>
        [Column(TypeName = "decimal(7,4)")]
        public decimal ForecastError { get; set; }

        /// <summary>Week / Month</summary>
        [MaxLength(10)]
        public string Granularity { get; set; } = "Month";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Dynamic reorder levels per item (optionally per location).</summary>
    public class ReorderPoint
    {
        [Key]
        public int ReorderPointId { get; set; }

        [ForeignKey(nameof(FinishedGood))]
        public int ItemId { get; set; }
        public FinishedGood FinishedGood { get; set; } = null!;

        [ForeignKey(nameof(Location))]
        public int? LocationId { get; set; }
        public InventoryLocation? Location { get; set; }

        /// <summary>Trigger a replenishment order when stock reaches this level.</summary>
        public int ReorderLevel { get; set; }

        /// <summary>Optimal order quantity (EOQ-derived).</summary>
        public int OrderQuantity { get; set; }

        /// <summary>Safety buffer stock to absorb demand spikes.</summary>
        public int SafetyStock { get; set; }

        /// <summary>Supplier lead time in days.</summary>
        public int LeadTimeDays { get; set; }

        /// <summary>ABC classification: A (High) / B (Medium) / C (Low)</summary>
        [MaxLength(1)]
        public string AbcCategory { get; set; } = "C";

        public DateTime LastCalculated { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Barcode tracking entry associated with a finished good.</summary>
    public class BarcodeInfo
    {
        [Key]
        public int BarcodeId { get; set; }

        [Required, MaxLength(100)]
        public string BarcodeValue { get; set; } = string.Empty;

        /// <summary>EAN13 / Code128 / QRCode / RFID</summary>
        [MaxLength(20)]
        public string BarcodeType { get; set; } = "EAN13";

        [ForeignKey(nameof(FinishedGood))]
        public int? ItemId { get; set; }
        public FinishedGood? FinishedGood { get; set; }

        [ForeignKey(nameof(Location))]
        public int? LocationId { get; set; }
        public InventoryLocation? Location { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime PrintedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? PrintedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Complete sales transaction record for reporting and forecasting input.</summary>
    public class SalesHistory
    {
        [Key]
        public int SalesHistoryId { get; set; }

        [Required, MaxLength(30)]
        public string InvoiceNo { get; set; } = string.Empty;

        [ForeignKey(nameof(FinishedGood))]
        public int ItemId { get; set; }
        public FinishedGood FinishedGood { get; set; } = null!;

        [ForeignKey(nameof(Location))]
        public int LocationId { get; set; }
        public InventoryLocation Location { get; set; } = null!;

        [ForeignKey(nameof(Customer))]
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(16,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GSTAmount { get; set; }

        public DateTime SaleDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Storage capacity constraints per item category at a location.</summary>
    public class LocationCapacity
    {
        [Key]
        public int LocationCapacityId { get; set; }

        [ForeignKey(nameof(Location))]
        public int LocationId { get; set; }
        public InventoryLocation Location { get; set; } = null!;

        [ForeignKey(nameof(Category))]
        public int? CategoryId { get; set; }
        public ItemCategory? Category { get; set; }

        /// <summary>Maximum number of units that can be stored.</summary>
        public int MaxCapacity { get; set; }

        /// <summary>Warning threshold (e.g., 80% of MaxCapacity).</summary>
        public int WarningThreshold { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
