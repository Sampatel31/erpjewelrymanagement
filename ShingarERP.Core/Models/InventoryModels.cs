using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShingarERP.Core.Models
{
    // ─────────────────────────────────────────────────────────────
    // Module 01 – Gold & Metal Inventory
    // ─────────────────────────────────────────────────────────────

    /// <summary>Metal master (Gold, Silver, Platinum …)</summary>
    public class Metal
    {
        [Key]
        public int MetalId { get; set; }

        [Required, MaxLength(50)]
        public string MetalType { get; set; } = string.Empty;   // Gold / Silver / Platinum

        [Required, MaxLength(20)]
        public string PurityCode { get; set; } = string.Empty;  // 24K, 22K, 18K …

        /// <summary>Fineness value (e.g. 999, 916, 750)</summary>
        [Column(TypeName = "decimal(7,3)")]
        public decimal Fineness { get; set; }

        [MaxLength(20)]
        public string WeightUnit { get; set; } = "g";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<MetalLot>     MetalLots     { get; set; } = new List<MetalLot>();
        public ICollection<MetalRate>    MetalRates    { get; set; } = new List<MetalRate>();
        public ICollection<MetalPurchase> MetalPurchases { get; set; } = new List<MetalPurchase>();
    }

    /// <summary>Purchase lot of raw metal from a supplier.</summary>
    public class MetalLot
    {
        [Key]
        public int LotId { get; set; }

        [Required, MaxLength(30)]
        public string LotNumber { get; set; } = string.Empty;

        [ForeignKey(nameof(Metal))]
        public int MetalId { get; set; }
        public Metal Metal { get; set; } = null!;

        [ForeignKey(nameof(Supplier))]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        [Column(TypeName = "decimal(12,4)")]
        public decimal GrossWeight { get; set; }

        [Column(TypeName = "decimal(12,4)")]
        public decimal NetWeight { get; set; }

        /// <summary>Melting loss % = (GrossWeight - NetWeight) / GrossWeight * 100</summary>
        [Column(TypeName = "decimal(7,4)")]
        public decimal MeltingLossPercent { get; set; }

        [Column(TypeName = "decimal(12,4)")]
        public decimal RemainingWeight { get; set; }

        [Column(TypeName = "decimal(14,4)")]
        public decimal PurchaseRatePerGram { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal TotalCost { get; set; }

        public DateTime PurchaseDate { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Live / manual MCX gold/silver rate per date.</summary>
    public class MetalRate
    {
        [Key]
        public int RateId { get; set; }

        [ForeignKey(nameof(Metal))]
        public int MetalId { get; set; }
        public Metal Metal { get; set; } = null!;

        [Column(TypeName = "decimal(14,4)")]
        public decimal RatePerGram { get; set; }

        [Column(TypeName = "decimal(14,4)")]
        public decimal RatePerTola { get; set; }

        [Column(TypeName = "decimal(14,4)")]
        public decimal MCXSpotRate { get; set; }

        public DateTime RateDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(20)]
        public string Source { get; set; } = "Manual"; // Manual / MCX-API
    }

    /// <summary>Purchase record – supplier-wise metal purchase ledger.</summary>
    public class MetalPurchase
    {
        [Key]
        public int PurchaseId { get; set; }

        [Required, MaxLength(30)]
        public string VoucherNo { get; set; } = string.Empty;

        [ForeignKey(nameof(Metal))]
        public int MetalId { get; set; }
        public Metal Metal { get; set; } = null!;

        [ForeignKey(nameof(Supplier))]
        public int SupplierId { get; set; }
        public Supplier Supplier { get; set; } = null!;

        [ForeignKey(nameof(MetalLot))]
        public int? LotId { get; set; }
        public MetalLot? MetalLot { get; set; }

        [Column(TypeName = "decimal(12,4)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(14,4)")]
        public decimal UnitRate { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal GSTAmount { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal NetAmount { get; set; }

        public DateTime PurchaseDate { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ─────────────────────────────────────────────────────────────
    // Module 02 – Finished Goods Inventory
    // ─────────────────────────────────────────────────────────────

    /// <summary>Category master for finished goods (Ring, Necklace, Bangle …)</summary>
    public class ItemCategory
    {
        [Key]
        public int CategoryId { get; set; }

        [Required, MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string CategoryCode { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<FinishedGood> FinishedGoods { get; set; } = new List<FinishedGood>();
    }

    /// <summary>SKU master for finished jewellery items.</summary>
    public class FinishedGood
    {
        [Key]
        public int ItemId { get; set; }

        [Required, MaxLength(30)]
        public string SKU { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string ItemName { get; set; } = string.Empty;

        [ForeignKey(nameof(Category))]
        public int CategoryId { get; set; }
        public ItemCategory Category { get; set; } = null!;

        [ForeignKey(nameof(Metal))]
        public int MetalId { get; set; }
        public Metal Metal { get; set; } = null!;

        [Column(TypeName = "decimal(7,4)")]
        public decimal GrossWeight { get; set; }

        [Column(TypeName = "decimal(7,4)")]
        public decimal NetWeight { get; set; }

        [Column(TypeName = "decimal(7,4)")]
        public decimal StoneWeight { get; set; }

        [Column(TypeName = "decimal(14,2)")]
        public decimal MakingChargePerGram { get; set; }

        [Column(TypeName = "decimal(14,2)")]
        public decimal MakingChargePercent { get; set; }

        [MaxLength(50)]
        public string? BarcodeNumber { get; set; }

        [MaxLength(100)]
        public string? PhotoPath { get; set; }

        [MaxLength(50)]
        public string? RFIDTag { get; set; }

        [MaxLength(50)]
        public string StockLocation { get; set; } = "Showroom";

        public int StockQuantity { get; set; } = 1;

        [Column(TypeName = "decimal(16,2)")]
        public decimal SalePrice { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<StockTransaction>    StockTransactions { get; set; } = new List<StockTransaction>();
        public ICollection<FinishedGoodStone>   Stones            { get; set; } = new List<FinishedGoodStone>();
    }

    /// <summary>Stock transaction record (in / out / transfer)</summary>
    public class StockTransaction
    {
        [Key]
        public int TransactionId { get; set; }

        [ForeignKey(nameof(FinishedGood))]
        public int ItemId { get; set; }
        public FinishedGood FinishedGood { get; set; } = null!;

        [Required, MaxLength(30)]
        public string VoucherNo { get; set; } = string.Empty;

        [MaxLength(20)]
        public string TransactionType { get; set; } = string.Empty; // Purchase/Sale/Transfer …

        public int QuantityIn  { get; set; }
        public int QuantityOut { get; set; }

        [MaxLength(50)]
        public string? FromLocation { get; set; }

        [MaxLength(50)]
        public string? ToLocation { get; set; }

        public DateTime TransactionDate { get; set; }

        [MaxLength(500)]
        public string? Remarks { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ─────────────────────────────────────────────────────────────
    // Module 04 – Stone & Diamond Inventory
    // ─────────────────────────────────────────────────────────────

    /// <summary>Stone/Diamond master with 4Cs and certificate data.</summary>
    public class Stone
    {
        [Key]
        public int StoneId { get; set; }

        [Required, MaxLength(30)]
        public string StoneCode { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string StoneType { get; set; } = string.Empty; // Diamond, Emerald, Ruby …

        [MaxLength(20)]
        public string? CertificateNo { get; set; }

        [MaxLength(20)]
        public string? CertLab { get; set; }   // GIA / IGI / HRD

        [Column(TypeName = "decimal(9,4)")]
        public decimal CaratWeight { get; set; }

        [MaxLength(10)]
        public string? Color { get; set; }

        [MaxLength(10)]
        public string? Clarity { get; set; }

        [MaxLength(20)]
        public string? Cut { get; set; }

        [MaxLength(20)]
        public string? Shape { get; set; }

        [MaxLength(50)]
        public string? Fluorescence { get; set; }

        [Column(TypeName = "decimal(7,2)")]
        public decimal Length { get; set; }

        [Column(TypeName = "decimal(7,2)")]
        public decimal Width { get; set; }

        [Column(TypeName = "decimal(7,2)")]
        public decimal Depth { get; set; }

        [Column(TypeName = "decimal(14,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(14,2)")]
        public decimal SalePrice { get; set; }

        [MaxLength(200)]
        public string? CertificatePath { get; set; }

        public bool IsConsignment { get; set; }

        [ForeignKey(nameof(Supplier))]
        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = "Available"; // Available / Allocated / Sold

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Link between a finished good item and its stones.</summary>
    public class FinishedGoodStone
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(FinishedGood))]
        public int ItemId { get; set; }
        public FinishedGood FinishedGood { get; set; } = null!;

        [ForeignKey(nameof(Stone))]
        public int StoneId { get; set; }
        public Stone Stone { get; set; } = null!;

        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(9,4)")]
        public decimal TotalCaratWeight { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    // Supplier master (shared across modules)
    // ─────────────────────────────────────────────────────────────

    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [Required, MaxLength(100)]
        public string SupplierName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? GSTIN { get; set; }

        [MaxLength(20)]
        public string? PAN { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditLimit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingBalance { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<MetalLot>      MetalLots      { get; set; } = new List<MetalLot>();
        public ICollection<MetalPurchase> MetalPurchases { get; set; } = new List<MetalPurchase>();
        public ICollection<Stone>         Stones         { get; set; } = new List<Stone>();
    }
}
