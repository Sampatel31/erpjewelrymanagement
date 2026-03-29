using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShingarERP.Core.Models
{
    // ─────────────────────────────────────────────────────────────
    // Module 22 – Accounts & Ledger (Double-Entry)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Chart of Accounts master.</summary>
    public class Account
    {
        [Key]
        public int AccountId { get; set; }

        [Required, MaxLength(20)]
        public string AccountCode { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string AccountName { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string AccountType { get; set; } = string.Empty; // Asset/Liability/Equity/Revenue/Expense

        [MaxLength(50)]
        public string? AccountGroup { get; set; }

        /// <summary>Parent account for hierarchical COA.</summary>
        public int? ParentAccountId { get; set; }
        public Account? ParentAccount { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal OpeningBalance { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal CurrentBalance { get; set; }

        [MaxLength(10)]
        public string NormalBalance { get; set; } = "Dr"; // Dr / Cr

        public bool IsControl { get; set; }     // Control / summary account
        public bool AllowPosting { get; set; } = true;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Account>        ChildAccounts  { get; set; } = new List<Account>();
        public ICollection<JournalEntryLine> EntryLines   { get; set; } = new List<JournalEntryLine>();
    }

    /// <summary>Journal voucher header (double-entry).</summary>
    public class JournalEntry
    {
        [Key]
        public int EntryId { get; set; }

        [Required, MaxLength(30)]
        public string VoucherNo { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string VoucherType { get; set; } = string.Empty; // Cash/Bank/Journal …

        public DateTime VoucherDate { get; set; }

        [MaxLength(500)]
        public string? Narration { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal TotalDebit { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal TotalCredit { get; set; }

        public bool IsPosted { get; set; } = false;
        public bool IsReversed { get; set; } = false;

        public int? CreatedByUserId { get; set; }

        [MaxLength(30)]
        public string? ReferenceNo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
    }

    /// <summary>Individual debit/credit line in a journal entry.</summary>
    public class JournalEntryLine
    {
        [Key]
        public int LineId { get; set; }

        [ForeignKey(nameof(JournalEntry))]
        public int EntryId { get; set; }
        public JournalEntry JournalEntry { get; set; } = null!;

        [ForeignKey(nameof(Account))]
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;

        [Column(TypeName = "decimal(18,4)")]
        public decimal DebitAmount { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal CreditAmount { get; set; }

        [MaxLength(300)]
        public string? Narration { get; set; }

        public int SortOrder { get; set; }
    }

    /// <summary>Day-end cash/bank book summary.</summary>
    public class DayBook
    {
        [Key]
        public int DayBookId { get; set; }

        public DateTime BookDate { get; set; }

        [MaxLength(20)]
        public string BookType { get; set; } = "Cash"; // Cash / Bank

        [ForeignKey(nameof(Account))]
        public int AccountId { get; set; }
        public Account Account { get; set; } = null!;

        [Column(TypeName = "decimal(18,4)")]
        public decimal OpeningBalance { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal TotalReceipts { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal TotalPayments { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ClosingBalance { get; set; }

        public bool IsClosed { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
