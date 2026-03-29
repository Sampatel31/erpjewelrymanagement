using System;
using System.ComponentModel.DataAnnotations;

namespace ShingarERP.Core.DTOs
{
    // ─────────────────────────────────────────────────────────────
    // Inventory DTOs
    // ─────────────────────────────────────────────────────────────

    public class MetalDto
    {
        public int    MetalId    { get; set; }
        public string MetalType  { get; set; } = string.Empty;
        public string PurityCode { get; set; } = string.Empty;
        public decimal Fineness  { get; set; }
        public string WeightUnit { get; set; } = "g";
        public bool   IsActive   { get; set; }
    }

    public class MetalLotDto
    {
        public int     LotId              { get; set; }
        public string  LotNumber          { get; set; } = string.Empty;
        public int     MetalId            { get; set; }
        public string  MetalName          { get; set; } = string.Empty;
        public int     SupplierId         { get; set; }
        public string  SupplierName       { get; set; } = string.Empty;
        public decimal GrossWeight        { get; set; }
        public decimal NetWeight          { get; set; }
        public decimal MeltingLossPercent { get; set; }
        public decimal RemainingWeight    { get; set; }
        public decimal PurchaseRatePerGram{ get; set; }
        public decimal TotalCost          { get; set; }
        public DateTime PurchaseDate      { get; set; }
        public string?  Remarks           { get; set; }
    }

    public class CreateMetalLotRequest
    {
        [Required]
        public string  LotNumber           { get; set; } = string.Empty;
        [Required]
        public int     MetalId             { get; set; }
        [Required]
        public int     SupplierId          { get; set; }
        [Range(0.001, 999999)]
        public decimal GrossWeight         { get; set; }
        [Range(0.001, 999999)]
        public decimal NetWeight           { get; set; }
        [Range(0, 50)]
        public decimal MeltingLossPercent  { get; set; }
        [Range(0.01, 9999999)]
        public decimal PurchaseRatePerGram { get; set; }
        public DateTime PurchaseDate       { get; set; }
        public string?  Remarks            { get; set; }
    }

    public class MetalRateDto
    {
        public int     RateId        { get; set; }
        public int     MetalId       { get; set; }
        public string  MetalName     { get; set; } = string.Empty;
        public decimal RatePerGram   { get; set; }
        public decimal RatePerTola   { get; set; }
        public decimal MCXSpotRate   { get; set; }
        public DateTime RateDate     { get; set; }
        public string  Source        { get; set; } = "Manual";
    }

    public class CreateMetalRateRequest
    {
        [Required]
        public int     MetalId       { get; set; }
        [Range(1, 9999999)]
        public decimal RatePerGram   { get; set; }
        [Range(1, 9999999)]
        public decimal MCXSpotRate   { get; set; }
        public DateTime RateDate     { get; set; }
        public string  Source        { get; set; } = "Manual";
    }

    public class FinishedGoodDto
    {
        public int     ItemId               { get; set; }
        public string  SKU                  { get; set; } = string.Empty;
        public string  ItemName             { get; set; } = string.Empty;
        public int     CategoryId           { get; set; }
        public string  CategoryName         { get; set; } = string.Empty;
        public int     MetalId              { get; set; }
        public string  MetalName            { get; set; } = string.Empty;
        public decimal GrossWeight          { get; set; }
        public decimal NetWeight            { get; set; }
        public decimal StoneWeight          { get; set; }
        public decimal MakingChargePerGram  { get; set; }
        public decimal MakingChargePercent  { get; set; }
        public string? BarcodeNumber        { get; set; }
        public string? PhotoPath            { get; set; }
        public string  StockLocation        { get; set; } = "Showroom";
        public int     StockQuantity        { get; set; }
        public decimal SalePrice            { get; set; }
        public string? Description          { get; set; }
        public bool    IsActive             { get; set; }
    }

    public class CreateFinishedGoodRequest
    {
        [Required, MaxLength(30)]
        public string  SKU                  { get; set; } = string.Empty;
        [Required, MaxLength(200)]
        public string  ItemName             { get; set; } = string.Empty;
        [Required]
        public int     CategoryId           { get; set; }
        [Required]
        public int     MetalId              { get; set; }
        [Range(0.001, 9999)]
        public decimal GrossWeight          { get; set; }
        [Range(0.001, 9999)]
        public decimal NetWeight            { get; set; }
        public decimal StoneWeight          { get; set; }
        public decimal MakingChargePerGram  { get; set; }
        public decimal MakingChargePercent  { get; set; }
        public string? BarcodeNumber        { get; set; }
        public string  StockLocation        { get; set; } = "Showroom";
        [Range(0.01, 9999999)]
        public decimal SalePrice            { get; set; }
        public string? Description          { get; set; }
    }

    public class StoneDto
    {
        public int     StoneId         { get; set; }
        public string  StoneCode       { get; set; } = string.Empty;
        public string  StoneType       { get; set; } = string.Empty;
        public string? CertificateNo   { get; set; }
        public string? CertLab         { get; set; }
        public decimal CaratWeight     { get; set; }
        public string? Color           { get; set; }
        public string? Clarity         { get; set; }
        public string? Cut             { get; set; }
        public string? Shape           { get; set; }
        public decimal PurchasePrice   { get; set; }
        public decimal SalePrice       { get; set; }
        public bool    IsConsignment   { get; set; }
        public string  Status          { get; set; } = "Available";
    }

    public class CreateStoneRequest
    {
        [Required, MaxLength(30)]
        public string  StoneCode       { get; set; } = string.Empty;
        [Required, MaxLength(50)]
        public string  StoneType       { get; set; } = string.Empty;
        public string? CertificateNo   { get; set; }
        public string? CertLab         { get; set; }
        [Range(0.001, 9999)]
        public decimal CaratWeight     { get; set; }
        public string? Color           { get; set; }
        public string? Clarity         { get; set; }
        public string? Cut             { get; set; }
        public string? Shape           { get; set; }
        [Range(0.01, 9999999)]
        public decimal PurchasePrice   { get; set; }
        [Range(0.01, 9999999)]
        public decimal SalePrice       { get; set; }
        public bool    IsConsignment   { get; set; }
        public int?    SupplierId      { get; set; }
    }

    public class SupplierDto
    {
        public int     SupplierId       { get; set; }
        public string  SupplierName     { get; set; } = string.Empty;
        public string? Address          { get; set; }
        public string? Phone            { get; set; }
        public string? Email            { get; set; }
        public string? GSTIN            { get; set; }
        public string? PAN              { get; set; }
        public decimal CreditLimit      { get; set; }
        public decimal OutstandingBalance { get; set; }
        public bool    IsActive         { get; set; }
    }

    public class StockAdjustmentRequest
    {
        [Required]
        public int    ItemId           { get; set; }
        [Required]
        public string AdjustmentType   { get; set; } = string.Empty;
        public int    QuantityChange    { get; set; }
        public string? FromLocation    { get; set; }
        public string? ToLocation      { get; set; }
        public string? Remarks         { get; set; }
    }
}
