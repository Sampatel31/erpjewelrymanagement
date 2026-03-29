using Microsoft.EntityFrameworkCore;
using ShingarERP.Core.Models;

namespace ShingarERP.Data
{
    /// <summary>
    /// Entity Framework Core DbContext for Shringar Jewellers ERP.
    /// Covers Modules 01, 02, 04, 15, 22 (Phase 1).
    /// </summary>
    public class ShingarContext : DbContext
    {
        public ShingarContext(DbContextOptions<ShingarContext> options) : base(options) { }

        // ── Module 01: Metal Inventory ──────────────────────────────
        public DbSet<Metal>        Metals        { get; set; }
        public DbSet<MetalLot>     MetalLots     { get; set; }
        public DbSet<MetalRate>    MetalRates    { get; set; }
        public DbSet<MetalPurchase> MetalPurchases { get; set; }

        // ── Module 02: Finished Goods ───────────────────────────────
        public DbSet<ItemCategory>    ItemCategories  { get; set; }
        public DbSet<FinishedGood>    FinishedGoods   { get; set; }
        public DbSet<StockTransaction> StockTransactions { get; set; }

        // ── Module 04: Stone & Diamond ──────────────────────────────
        public DbSet<Stone>            Stones          { get; set; }
        public DbSet<FinishedGoodStone> FinishedGoodStones { get; set; }

        // ── Shared master ───────────────────────────────────────────
        public DbSet<Supplier> Suppliers { get; set; }

        // ── Module 15: Customer ─────────────────────────────────────
        public DbSet<Customer>         Customers        { get; set; }
        public DbSet<CustomerDocument> CustomerDocuments { get; set; }
        public DbSet<FamilyMember>     FamilyMembers    { get; set; }

        // ── Module 22: Accounting ───────────────────────────────────
        public DbSet<Account>          Accounts         { get; set; }
        public DbSet<JournalEntry>     JournalEntries   { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
        public DbSet<DayBook>          DayBooks         { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Metal ────────────────────────────────────────────────
            modelBuilder.Entity<Metal>(e =>
            {
                e.HasIndex(x => new { x.MetalType, x.PurityCode }).IsUnique();
                e.Property(x => x.Fineness).HasPrecision(7, 3);
            });

            // ── MetalLot ─────────────────────────────────────────────
            modelBuilder.Entity<MetalLot>(e =>
            {
                e.HasIndex(x => x.LotNumber).IsUnique();
                e.Property(x => x.GrossWeight).HasPrecision(12, 4);
                e.Property(x => x.NetWeight).HasPrecision(12, 4);
                e.Property(x => x.RemainingWeight).HasPrecision(12, 4);
                e.Property(x => x.PurchaseRatePerGram).HasPrecision(14, 4);
                e.Property(x => x.TotalCost).HasPrecision(18, 4);

                e.HasOne(x => x.Metal)
                 .WithMany(m => m.MetalLots)
                 .HasForeignKey(x => x.MetalId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Supplier)
                 .WithMany(s => s.MetalLots)
                 .HasForeignKey(x => x.SupplierId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── MetalRate ────────────────────────────────────────────
            modelBuilder.Entity<MetalRate>(e =>
            {
                e.HasIndex(x => new { x.MetalId, x.RateDate });
                e.Property(x => x.RatePerGram).HasPrecision(14, 4);
                e.Property(x => x.RatePerTola).HasPrecision(14, 4);
                e.Property(x => x.MCXSpotRate).HasPrecision(14, 4);

                e.HasOne(x => x.Metal)
                 .WithMany(m => m.MetalRates)
                 .HasForeignKey(x => x.MetalId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── MetalPurchase ────────────────────────────────────────
            modelBuilder.Entity<MetalPurchase>(e =>
            {
                e.HasIndex(x => x.VoucherNo).IsUnique();
                e.Property(x => x.Quantity).HasPrecision(12, 4);
                e.Property(x => x.UnitRate).HasPrecision(14, 4);
                e.Property(x => x.TotalAmount).HasPrecision(18, 4);
                e.Property(x => x.GSTAmount).HasPrecision(18, 4);
                e.Property(x => x.NetAmount).HasPrecision(18, 4);

                e.HasOne(x => x.Metal)
                 .WithMany(m => m.MetalPurchases)
                 .HasForeignKey(x => x.MetalId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Supplier)
                 .WithMany(s => s.MetalPurchases)
                 .HasForeignKey(x => x.SupplierId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Finished Good ────────────────────────────────────────
            modelBuilder.Entity<FinishedGood>(e =>
            {
                e.HasIndex(x => x.SKU).IsUnique();
                e.HasIndex(x => x.BarcodeNumber);
                e.Property(x => x.GrossWeight).HasPrecision(7, 4);
                e.Property(x => x.NetWeight).HasPrecision(7, 4);
                e.Property(x => x.StoneWeight).HasPrecision(7, 4);
                e.Property(x => x.MakingChargePerGram).HasPrecision(14, 2);
                e.Property(x => x.MakingChargePercent).HasPrecision(14, 2);
                e.Property(x => x.SalePrice).HasPrecision(16, 2);

                e.HasOne(x => x.Category)
                 .WithMany(c => c.FinishedGoods)
                 .HasForeignKey(x => x.CategoryId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Metal)
                 .WithMany()
                 .HasForeignKey(x => x.MetalId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Stone ────────────────────────────────────────────────
            modelBuilder.Entity<Stone>(e =>
            {
                e.HasIndex(x => x.StoneCode).IsUnique();
                e.HasIndex(x => x.CertificateNo);
                e.Property(x => x.CaratWeight).HasPrecision(9, 4);
                e.Property(x => x.PurchasePrice).HasPrecision(14, 2);
                e.Property(x => x.SalePrice).HasPrecision(14, 2);

                e.HasOne(x => x.Supplier)
                 .WithMany(s => s.Stones)
                 .HasForeignKey(x => x.SupplierId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── FinishedGoodStone ────────────────────────────────────
            modelBuilder.Entity<FinishedGoodStone>(e =>
            {
                e.HasOne(x => x.FinishedGood)
                 .WithMany(f => f.Stones)
                 .HasForeignKey(x => x.ItemId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Stone)
                 .WithMany()
                 .HasForeignKey(x => x.StoneId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Supplier ─────────────────────────────────────────────
            modelBuilder.Entity<Supplier>(e =>
            {
                e.HasIndex(x => x.SupplierName);
                e.HasIndex(x => x.GSTIN);
                e.Property(x => x.CreditLimit).HasPrecision(18, 2);
                e.Property(x => x.OutstandingBalance).HasPrecision(18, 2);
            });

            // ── Customer ─────────────────────────────────────────────
            modelBuilder.Entity<Customer>(e =>
            {
                e.HasIndex(x => x.Mobile);
                e.HasIndex(x => x.AadhaarNumber);
                e.HasIndex(x => x.PANNumber);
                e.HasIndex(x => x.CustomerCode).IsUnique().HasFilter("[CustomerCode] IS NOT NULL");
                e.Property(x => x.LTVScore).HasPrecision(8, 2);
                e.Property(x => x.TotalPurchaseAmount).HasPrecision(18, 2);

                // Ignore computed property
                e.Ignore(x => x.FullName);
            });

            // ── Account ──────────────────────────────────────────────
            modelBuilder.Entity<Account>(e =>
            {
                e.HasIndex(x => x.AccountCode).IsUnique();
                e.Property(x => x.OpeningBalance).HasPrecision(18, 4);
                e.Property(x => x.CurrentBalance).HasPrecision(18, 4);

                // Self-referencing hierarchy
                e.HasOne(x => x.ParentAccount)
                 .WithMany(x => x.ChildAccounts)
                 .HasForeignKey(x => x.ParentAccountId)
                 .OnDelete(DeleteBehavior.Restrict)
                 .IsRequired(false);
            });

            // ── JournalEntry ─────────────────────────────────────────
            modelBuilder.Entity<JournalEntry>(e =>
            {
                e.HasIndex(x => x.VoucherNo).IsUnique();
                e.HasIndex(x => x.VoucherDate);
                e.Property(x => x.TotalDebit).HasPrecision(18, 4);
                e.Property(x => x.TotalCredit).HasPrecision(18, 4);
            });

            // ── JournalEntryLine ─────────────────────────────────────
            modelBuilder.Entity<JournalEntryLine>(e =>
            {
                e.Property(x => x.DebitAmount).HasPrecision(18, 4);
                e.Property(x => x.CreditAmount).HasPrecision(18, 4);

                e.HasOne(x => x.JournalEntry)
                 .WithMany(j => j.Lines)
                 .HasForeignKey(x => x.EntryId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Account)
                 .WithMany(a => a.EntryLines)
                 .HasForeignKey(x => x.AccountId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── DayBook ──────────────────────────────────────────────
            modelBuilder.Entity<DayBook>(e =>
            {
                e.HasIndex(x => new { x.BookDate, x.BookType, x.AccountId }).IsUnique();
                e.Property(x => x.OpeningBalance).HasPrecision(18, 4);
                e.Property(x => x.TotalReceipts).HasPrecision(18, 4);
                e.Property(x => x.TotalPayments).HasPrecision(18, 4);
                e.Property(x => x.ClosingBalance).HasPrecision(18, 4);

                e.HasOne(x => x.Account)
                 .WithMany()
                 .HasForeignKey(x => x.AccountId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Seed data ────────────────────────────────────────────
            SeedData(modelBuilder);
        }

        private static void SeedData(ModelBuilder modelBuilder)
        {
            // Seed metal types
            modelBuilder.Entity<Metal>().HasData(
                new Metal { MetalId = 1, MetalType = "Gold",   PurityCode = "24K", Fineness = 999.0m, WeightUnit = "g" },
                new Metal { MetalId = 2, MetalType = "Gold",   PurityCode = "22K", Fineness = 916.0m, WeightUnit = "g" },
                new Metal { MetalId = 3, MetalType = "Gold",   PurityCode = "18K", Fineness = 750.0m, WeightUnit = "g" },
                new Metal { MetalId = 4, MetalType = "Silver", PurityCode = "99.9", Fineness = 999.0m, WeightUnit = "g" },
                new Metal { MetalId = 5, MetalType = "Silver", PurityCode = "92.5", Fineness = 925.0m, WeightUnit = "g" },
                new Metal { MetalId = 6, MetalType = "Platinum",PurityCode = "95", Fineness = 950.0m, WeightUnit = "g" }
            );

            // Seed item categories
            modelBuilder.Entity<ItemCategory>().HasData(
                new ItemCategory { CategoryId = 1, CategoryName = "Ring",     CategoryCode = "RNG" },
                new ItemCategory { CategoryId = 2, CategoryName = "Necklace", CategoryCode = "NCK" },
                new ItemCategory { CategoryId = 3, CategoryName = "Earring",  CategoryCode = "ERG" },
                new ItemCategory { CategoryId = 4, CategoryName = "Bangle",   CategoryCode = "BNG" },
                new ItemCategory { CategoryId = 5, CategoryName = "Bracelet", CategoryCode = "BRC" },
                new ItemCategory { CategoryId = 6, CategoryName = "Pendant",  CategoryCode = "PDT" },
                new ItemCategory { CategoryId = 7, CategoryName = "Chain",    CategoryCode = "CHN" },
                new ItemCategory { CategoryId = 8, CategoryName = "Mangalsutra", CategoryCode = "MNG" }
            );

            // Seed core accounts (Chart of Accounts)
            modelBuilder.Entity<Account>().HasData(
                // Assets
                new Account { AccountId = 1,  AccountCode = "1000", AccountName = "Assets",              AccountType = "Asset",     NormalBalance = "Dr", IsControl = true, AllowPosting = false },
                new Account { AccountId = 2,  AccountCode = "1100", AccountName = "Current Assets",      AccountType = "Asset",     NormalBalance = "Dr", IsControl = true, AllowPosting = false, ParentAccountId = 1 },
                new Account { AccountId = 3,  AccountCode = "1101", AccountName = "Cash in Hand",        AccountType = "Asset",     NormalBalance = "Dr", ParentAccountId = 2 },
                new Account { AccountId = 4,  AccountCode = "1102", AccountName = "Bank Account",        AccountType = "Asset",     NormalBalance = "Dr", ParentAccountId = 2 },
                new Account { AccountId = 5,  AccountCode = "1200", AccountName = "Inventory",           AccountType = "Asset",     NormalBalance = "Dr", IsControl = true, AllowPosting = false, ParentAccountId = 1 },
                new Account { AccountId = 6,  AccountCode = "1201", AccountName = "Gold Stock",          AccountType = "Asset",     NormalBalance = "Dr", ParentAccountId = 5 },
                new Account { AccountId = 7,  AccountCode = "1202", AccountName = "Finished Goods Stock",AccountType = "Asset",     NormalBalance = "Dr", ParentAccountId = 5 },
                new Account { AccountId = 8,  AccountCode = "1203", AccountName = "Stone/Diamond Stock", AccountType = "Asset",     NormalBalance = "Dr", ParentAccountId = 5 },
                // Liabilities
                new Account { AccountId = 9,  AccountCode = "2000", AccountName = "Liabilities",         AccountType = "Liability", NormalBalance = "Cr", IsControl = true, AllowPosting = false },
                new Account { AccountId = 10, AccountCode = "2100", AccountName = "Accounts Payable",    AccountType = "Liability", NormalBalance = "Cr", ParentAccountId = 9 },
                new Account { AccountId = 11, AccountCode = "2200", AccountName = "GST Payable",         AccountType = "Liability", NormalBalance = "Cr", ParentAccountId = 9 },
                // Equity
                new Account { AccountId = 12, AccountCode = "3000", AccountName = "Owner Equity",        AccountType = "Equity",    NormalBalance = "Cr", IsControl = true, AllowPosting = false },
                new Account { AccountId = 13, AccountCode = "3100", AccountName = "Capital Account",     AccountType = "Equity",    NormalBalance = "Cr", ParentAccountId = 12 },
                new Account { AccountId = 14, AccountCode = "3200", AccountName = "Retained Earnings",   AccountType = "Equity",    NormalBalance = "Cr", ParentAccountId = 12 },
                // Revenue
                new Account { AccountId = 15, AccountCode = "4000", AccountName = "Revenue",             AccountType = "Revenue",   NormalBalance = "Cr", IsControl = true, AllowPosting = false },
                new Account { AccountId = 16, AccountCode = "4100", AccountName = "Sales – Jewellery",   AccountType = "Revenue",   NormalBalance = "Cr", ParentAccountId = 15 },
                new Account { AccountId = 17, AccountCode = "4200", AccountName = "Making Charge Income",AccountType = "Revenue",   NormalBalance = "Cr", ParentAccountId = 15 },
                // Expenses
                new Account { AccountId = 18, AccountCode = "5000", AccountName = "Expenses",            AccountType = "Expense",   NormalBalance = "Dr", IsControl = true, AllowPosting = false },
                new Account { AccountId = 19, AccountCode = "5100", AccountName = "Gold Purchase",       AccountType = "Expense",   NormalBalance = "Dr", ParentAccountId = 18 },
                new Account { AccountId = 20, AccountCode = "5200", AccountName = "Wages & Salaries",    AccountType = "Expense",   NormalBalance = "Dr", ParentAccountId = 18 },
                new Account { AccountId = 21, AccountCode = "5300", AccountName = "Rent Expense",        AccountType = "Expense",   NormalBalance = "Dr", ParentAccountId = 18 },
                new Account { AccountId = 22, AccountCode = "5400", AccountName = "Electricity Expense", AccountType = "Expense",   NormalBalance = "Dr", ParentAccountId = 18 }
            );
        }
    }
}
