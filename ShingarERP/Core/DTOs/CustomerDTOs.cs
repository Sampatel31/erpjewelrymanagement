using System;
using System.ComponentModel.DataAnnotations;

namespace ShingarERP.Core.DTOs
{
    // ─────────────────────────────────────────────────────────────
    // Customer DTOs
    // ─────────────────────────────────────────────────────────────

    public class CustomerDto
    {
        public int      CustomerId           { get; set; }
        public string   FirstName            { get; set; } = string.Empty;
        public string?  LastName             { get; set; }
        public string   FullName             { get; set; } = string.Empty;
        public string   Mobile               { get; set; } = string.Empty;
        public string?  Email                { get; set; }
        public string?  Address              { get; set; }
        public string?  City                 { get; set; }
        public string?  State                { get; set; }
        public string?  PinCode              { get; set; }
        public DateTime? DateOfBirth         { get; set; }
        public DateTime? AnniversaryDate     { get; set; }
        public string?  Gender               { get; set; }
        public string?  AadhaarNumber        { get; set; }
        public string?  PANNumber            { get; set; }
        public bool     KYCVerified          { get; set; }
        public DateTime? KYCVerifiedDate     { get; set; }
        public decimal  LTVScore             { get; set; }
        public decimal  TotalPurchaseAmount  { get; set; }
        public int      TotalPurchaseCount   { get; set; }
        public DateTime? LastPurchaseDate    { get; set; }
        public string?  CustomerCode         { get; set; }
        public bool     IsActive             { get; set; }
    }

    public class CreateCustomerRequest
    {
        [Required, MaxLength(100)]
        public string  FirstName        { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? LastName         { get; set; }

        [Required, MaxLength(15), Phone]
        public string  Mobile           { get; set; } = string.Empty;

        [MaxLength(100), EmailAddress]
        public string? Email            { get; set; }

        [MaxLength(500)]
        public string? Address          { get; set; }

        [MaxLength(100)]
        public string? City             { get; set; }

        [MaxLength(50)]
        public string? State            { get; set; }

        [MaxLength(10)]
        public string? PinCode          { get; set; }

        public DateTime? DateOfBirth    { get; set; }
        public DateTime? AnniversaryDate{ get; set; }

        [MaxLength(20)]
        public string? Gender           { get; set; }

        [MaxLength(12)]
        public string? AadhaarNumber    { get; set; }

        [MaxLength(10)]
        public string? PANNumber        { get; set; }
    }

    public class UpdateCustomerRequest : CreateCustomerRequest
    {
        [Required]
        public int CustomerId { get; set; }
    }

    public class KycVerificationRequest
    {
        [Required]
        public int     CustomerId    { get; set; }
        [Required]
        public string  DocumentType  { get; set; } = string.Empty;
        [Required]
        public string  DocumentNumber{ get; set; } = string.Empty;
        public string? FilePath      { get; set; }
    }

    public class CustomerSearchRequest
    {
        public string? SearchTerm    { get; set; }   // Name / Mobile / Code
        public string? City          { get; set; }
        public bool?   KYCVerified   { get; set; }
        public bool?   IsActive      { get; set; }
        public int     PageNumber    { get; set; } = 1;
        public int     PageSize      { get; set; } = 25;
    }

    public class PagedResult<T>
    {
        public System.Collections.Generic.IEnumerable<T> Items    { get; set; }
            = System.Linq.Enumerable.Empty<T>();
        public int TotalCount  { get; set; }
        public int PageNumber  { get; set; }
        public int PageSize    { get; set; }
        public int TotalPages  => (int)System.Math.Ceiling((double)TotalCount / PageSize);
    }
}
