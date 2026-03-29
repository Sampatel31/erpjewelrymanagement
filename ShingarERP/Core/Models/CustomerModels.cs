using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShingarERP.Core.Models
{
    // ─────────────────────────────────────────────────────────────
    // Module 15 – Customer Master & KYC
    // ─────────────────────────────────────────────────────────────

    /// <summary>Customer master with full KYC details.</summary>
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? LastName { get; set; }

        [MaxLength(201)]
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [Required, MaxLength(15)]
        public string Mobile { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(50)]
        public string? State { get; set; }

        [MaxLength(10)]
        public string? PinCode { get; set; }

        public DateTime? DateOfBirth { get; set; }
        public DateTime? AnniversaryDate { get; set; }

        [MaxLength(20)]
        public string? Gender { get; set; }

        // KYC
        [MaxLength(20)]
        public string? AadhaarNumber { get; set; }

        [MaxLength(10)]
        public string? PANNumber { get; set; }

        [MaxLength(30)]
        public string? OtherDocType { get; set; }

        [MaxLength(50)]
        public string? OtherDocNumber { get; set; }

        public bool KYCVerified { get; set; } = false;
        public DateTime? KYCVerifiedDate { get; set; }

        [MaxLength(200)]
        public string? PhotoPath { get; set; }

        // LTV (Lifetime Value) score (0-1000)
        [Column(TypeName = "decimal(8,2)")]
        public decimal LTVScore { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPurchaseAmount { get; set; }

        public int TotalPurchaseCount { get; set; }

        public DateTime? LastPurchaseDate { get; set; }

        // Referral
        public int? ReferredByCustomerId { get; set; }

        [MaxLength(30)]
        public string? CustomerCode { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<CustomerDocument>  Documents      { get; set; } = new List<CustomerDocument>();
        public ICollection<FamilyMember>      FamilyMembers  { get; set; } = new List<FamilyMember>();
    }

    /// <summary>KYC documents uploaded for a customer.</summary>
    public class CustomerDocument
    {
        [Key]
        public int DocumentId { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        [Required, MaxLength(30)]
        public string DocumentType { get; set; } = string.Empty; // Aadhaar / PAN / Passport …

        [Required, MaxLength(50)]
        public string DocumentNumber { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? FilePath { get; set; }

        public bool IsVerified { get; set; }
        public DateTime? VerifiedDate { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>Family member linked to a customer for cross-selling and gifting.</summary>
    public class FamilyMember
    {
        [Key]
        public int MemberId { get; set; }

        [ForeignKey(nameof(Customer))]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(30)]
        public string? Relationship { get; set; }

        public DateTime? DateOfBirth { get; set; }
        public DateTime? AnniversaryDate { get; set; }

        [MaxLength(15)]
        public string? Mobile { get; set; }
    }
}
