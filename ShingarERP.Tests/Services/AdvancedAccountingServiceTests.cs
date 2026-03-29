using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ShingarERP.Core.Models;
using ShingarERP.Data;
using ShingarERP.Data.Repositories;
using ShingarERP.Services;

namespace ShingarERP.Tests.Services
{
    [TestFixture]
    public class AdvancedAccountingServiceTests
    {
        private ShingarContext _context = null!;
        private UnitOfWork _uow = null!;
        private GeneralLedgerRepository _glRepo = null!;
        private TrialBalanceRepository _tbRepo = null!;
        private CostCenterRepository _ccRepo = null!;
        private BudgetRepository _budgetRepo = null!;
        private AdvancedAccountingService _service = null!;

        private int _accountId;

        [SetUp]
        public void SetUp()
        {
            var options = new DbContextOptionsBuilder<ShingarContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context    = new ShingarContext(options);
            _uow        = new UnitOfWork(_context);
            _glRepo     = new GeneralLedgerRepository(_context);
            _tbRepo     = new TrialBalanceRepository(_context);
            _ccRepo     = new CostCenterRepository(_context);
            _budgetRepo = new BudgetRepository(_context);
            _service    = new AdvancedAccountingService(
                _uow, _glRepo, _tbRepo, _ccRepo, _budgetRepo,
                NullLogger<AdvancedAccountingService>.Instance);

            SeedTestData();
        }

        [TearDown]
        public void TearDown()
        {
            _uow.Dispose();
            _context.Dispose();
        }

        private void SeedTestData()
        {
            var account = new Account
            {
                AccountCode  = "4100",
                AccountName  = "Sales Revenue",
                AccountType  = "Revenue",
                NormalBalance = "Cr",
                AllowPosting = true,
                IsActive     = true
            };
            _context.Accounts.Add(account);
            _context.SaveChanges();
            _accountId = account.AccountId;
        }

        // ── GL Tests ──────────────────────────────────────────────────

        [Test]
        public async Task PostToGL_Debit()
        {
            var entry = await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V001", "Sales", 1000, 0, "Test debit");
            Assert.That(entry.DebitAmount, Is.EqualTo(1000));
            Assert.That(entry.RunningBalance, Is.EqualTo(1000));
        }

        [Test]
        public async Task PostToGL_Credit()
        {
            var entry = await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V001", "Sales", 0, 500, "Test credit");
            Assert.That(entry.CreditAmount, Is.EqualTo(500));
            Assert.That(entry.RunningBalance, Is.EqualTo(-500));
        }

        [Test]
        public async Task GetAccountBalance_Debits()
        {
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow.AddDays(-1), "V001", "Sales", 1000, 0);
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow.AddDays(-1), "V002", "Sales", 500, 0);
            var balance = await _service.GetAccountBalanceAsync(_accountId, DateTime.UtcNow);
            Assert.That(balance, Is.EqualTo(1500));
        }

        [Test]
        public async Task GetAccountBalance_Credits()
        {
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow.AddDays(-1), "V001", "Sales", 0, 800);
            var balance = await _service.GetAccountBalanceAsync(_accountId, DateTime.UtcNow);
            Assert.That(balance, Is.EqualTo(-800));
        }

        [Test]
        public async Task GenerateTrialBalance_Valid()
        {
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V001", "Sales", 1000, 0);
            var rows = (await _service.GenerateTrialBalanceAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 1)).ToList();
            Assert.That(rows.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task ValidateTrialBalance_Balanced()
        {
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V001", "Sales", 500, 500);
            var result = await _service.ValidateTrialBalanceAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task ValidateTrialBalance_Unbalanced()
        {
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V001", "Sales", 1000, 0);
            var result = await _service.ValidateTrialBalanceAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task GenerateFinancialStatement_PL()
        {
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V001", "Sales", 0, 5000);
            var stmt = await _service.GenerateFinancialStatementAsync("ProfitLoss", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 1);
            Assert.That(stmt.StatementType, Is.EqualTo("ProfitLoss"));
            Assert.That(stmt.NetProfit, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task GenerateFinancialStatement_BS()
        {
            var stmt = await _service.GenerateFinancialStatementAsync("BalanceSheet", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 1);
            Assert.That(stmt.StatementType, Is.EqualTo("BalanceSheet"));
        }

        // ── Cost Centre Tests ─────────────────────────────────────────

        [Test]
        public async Task CreateCostCenter_Valid()
        {
            var cc = await _service.CreateCostCenterAsync("CC001", "Retail", "Department");
            Assert.That(cc.CostCenterCode, Is.EqualTo("CC001"));
            Assert.That(cc.IsActive, Is.True);
        }

        [Test]
        public async Task CreateCostCenter_DuplicateCode()
        {
            await _service.CreateCostCenterAsync("CC001", "Retail", "Department");
            Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateCostCenterAsync("CC001", "Wholesale", "Department"));
        }

        [Test]
        public async Task AllocateToCostCenter_Valid()
        {
            var cc = await _service.CreateCostCenterAsync("CC002", "Workshop", "Department");
            var updated = await _service.AllocateToCostCenterAsync(cc.Id, 10000);
            Assert.That(updated.ActualAmount, Is.EqualTo(10000));
        }

        [Test]
        public async Task GetBudgetVariance_Positive()
        {
            var cc = await _service.CreateCostCenterAsync("CC003", "Admin", "Department");
            await _service.CreateBudgetAllocationAsync(cc.Id, _accountId, 2024, 1, 50000);
            var variance = await _service.GetBudgetVarianceAsync(cc.Id, 2024);
            Assert.That(variance, Is.EqualTo(50000)); // No actual, variance = budget
        }

        [Test]
        public async Task GetBudgetVariance_Negative()
        {
            var cc = await _service.CreateCostCenterAsync("CC004", "Sales", "Department");
            var budget = await _service.CreateBudgetAllocationAsync(cc.Id, _accountId, 2024, 1, 30000);
            await _service.UpdateActualAmountAsync(budget.Id, 40000);
            var variance = await _service.GetBudgetVarianceAsync(cc.Id, 2024);
            Assert.That(variance, Is.EqualTo(-10000));
        }

        [Test]
        public async Task CreateBudgetAllocation_Valid()
        {
            var cc = await _service.CreateCostCenterAsync("CC005", "IT", "Department");
            var budget = await _service.CreateBudgetAllocationAsync(cc.Id, _accountId, 2024, 3, 20000);
            Assert.That(budget.BudgetedAmount, Is.EqualTo(20000));
            Assert.That(budget.FiscalYear, Is.EqualTo(2024));
        }

        [Test]
        public async Task UpdateActualAmount_Valid()
        {
            var cc = await _service.CreateCostCenterAsync("CC006", "Finance", "Department");
            var budget = await _service.CreateBudgetAllocationAsync(cc.Id, _accountId, 2024, 4, 10000);
            var updated = await _service.UpdateActualAmountAsync(budget.Id, 8000);
            Assert.That(updated.ActualAmount, Is.EqualTo(8000));
        }

        // ── Tax Tests ─────────────────────────────────────────────────

        [Test]
        public async Task CalculateGST_Intrastate()
        {
            var tax = await _service.CalculateGSTAsync(10000, 3, false);
            Assert.That(tax.TaxType, Is.EqualTo("GST"));
            Assert.That(tax.TaxAmount, Is.EqualTo(300));
            Assert.That(tax.CGSTAmount, Is.EqualTo(150));
            Assert.That(tax.SGSTAmount, Is.EqualTo(150));
            Assert.That(tax.IGSTAmount, Is.EqualTo(0));
        }

        [Test]
        public async Task CalculateGST_Interstate()
        {
            var tax = await _service.CalculateGSTAsync(10000, 3, true);
            Assert.That(tax.TaxType, Is.EqualTo("IGST"));
            Assert.That(tax.IGSTAmount, Is.EqualTo(300));
            Assert.That(tax.CGSTAmount, Is.EqualTo(0));
        }

        [Test]
        public async Task CalculateTDS_Valid()
        {
            var tax = await _service.CalculateTDSAsync(100000, 1, "PurchaseOrder", 1);
            Assert.That(tax.TaxType, Is.EqualTo("TDS"));
            Assert.That(tax.TDSAmount, Is.EqualTo(1000));
        }

        [Test]
        public async Task GetGLEntries_ByDateRange()
        {
            var from = DateTime.UtcNow.AddDays(-2);
            var to   = DateTime.UtcNow.AddDays(1);
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V003", "Sales", 500, 0);
            var entries = (await _service.GetGLEntriesAsync(_accountId, from, to)).ToList();
            Assert.That(entries.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task PostToGL_WithCostCenter()
        {
            var cc = await _service.CreateCostCenterAsync("CC007", "Retail2", "Department");
            var entry = await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V010", "Sales", 2000, 0, "With CC", cc.Id);
            Assert.That(entry.CostCenterId, Is.EqualTo(cc.Id));
        }

        [Test]
        public async Task GenerateTrialBalance_MultipleAccounts()
        {
            var acct2 = new Account { AccountCode = "5001", AccountName = "Purchases", AccountType = "Expense", NormalBalance = "Dr", AllowPosting = true, IsActive = true };
            _context.Accounts.Add(acct2);
            await _context.SaveChangesAsync();

            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V001", "Sales", 0, 1000);
            await _service.PostToGeneralLedgerAsync(acct2.AccountId, DateTime.UtcNow, "V001", "Purchase", 1000, 0);

            var rows = (await _service.GenerateTrialBalanceAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 1)).ToList();
            Assert.That(rows.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public async Task ValidateTrialBalance_EmptyPeriod()
        {
            var result = await _service.ValidateTrialBalanceAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-9));
            Assert.That(result, Is.True); // Empty = balanced
        }

        [Test]
        public async Task CreateCostCenter_WithParent()
        {
            var parent = await _service.CreateCostCenterAsync("CC100", "Parent", "Department");
            var child  = await _service.CreateCostCenterAsync("CC101", "Child", "Department", parent.Id);
            Assert.That(child.ParentCostCenterId, Is.EqualTo(parent.Id));
        }

        [Test]
        public async Task GetCostCenterChildren()
        {
            var parent = await _service.CreateCostCenterAsync("CC200", "Root", "Department");
            await _service.CreateCostCenterAsync("CC201", "Child1", "Department", parent.Id);
            await _service.CreateCostCenterAsync("CC202", "Child2", "Department", parent.Id);
            var children = (await _ccRepo.GetChildrenAsync(parent.Id)).ToList();
            Assert.That(children.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task BudgetAllocationOverBudget()
        {
            var cc = await _service.CreateCostCenterAsync("CC300", "OverBudget", "Department");
            var budget = await _service.CreateBudgetAllocationAsync(cc.Id, _accountId, 2024, 6, 5000);
            await _service.UpdateActualAmountAsync(budget.Id, 7000);
            var overBudget = (await _budgetRepo.GetOverBudgetAsync()).ToList();
            Assert.That(overBudget.Count, Is.GreaterThan(0));
        }

        [Test]
        public async Task GLEntries_Reconciliation()
        {
            var entry = await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V099", "Sales", 100, 0);
            Assert.That(entry.IsReconciled, Is.False);
        }

        [Test]
        public async Task TrialBalance_ClosingBalance()
        {
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V001", "Sales", 0, 500);
            var rows = (await _service.GenerateTrialBalanceAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 1)).ToList();
            var row = rows.FirstOrDefault(r => r.AccountId == _accountId);
            Assert.That(row, Is.Not.Null);
            Assert.That(row!.ClosingCredit, Is.EqualTo(500));
        }

        [Test]
        public async Task PeriodClose_Valid()
        {
            await _service.PostToGeneralLedgerAsync(_accountId, DateTime.UtcNow, "V001", "Sales", 500, 500);
            var result = await _service.PerformPeriodCloseAsync(DateTime.UtcNow.AddDays(1), 1);
            Assert.That(result, Is.True);
        }

        [Test]
        public async Task CalculateGST_ZeroRate()
        {
            var tax = await _service.CalculateGSTAsync(10000, 0, false);
            Assert.That(tax.TaxAmount, Is.EqualTo(0));
        }
    }
}
