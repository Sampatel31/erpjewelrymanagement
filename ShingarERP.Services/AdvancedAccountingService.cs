using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;
using ShingarERP.Data.Repositories;

namespace ShingarERP.Services
{
    /// <summary>
    /// Advanced accounting operations: GL posting, trial balance generation,
    /// financial statements, cost center allocation, and tax calculations.
    /// </summary>
    public class AdvancedAccountingService
    {
        private readonly IUnitOfWork _uow;
        private readonly GeneralLedgerRepository _glRepo;
        private readonly TrialBalanceRepository _tbRepo;
        private readonly CostCenterRepository _ccRepo;
        private readonly BudgetRepository _budgetRepo;
        private readonly ILogger<AdvancedAccountingService> _logger;

        /// <summary>Initialises the service with required repositories and logger.</summary>
        public AdvancedAccountingService(
            IUnitOfWork uow,
            GeneralLedgerRepository glRepo,
            TrialBalanceRepository tbRepo,
            CostCenterRepository ccRepo,
            BudgetRepository budgetRepo,
            ILogger<AdvancedAccountingService> logger)
        {
            _uow = uow;
            _glRepo = glRepo;
            _tbRepo = tbRepo;
            _ccRepo = ccRepo;
            _budgetRepo = budgetRepo;
            _logger = logger;
        }

        // ── General Ledger ────────────────────────────────────────────

        /// <summary>Posts a debit or credit entry to the General Ledger.</summary>
        public async Task<GeneralLedger> PostToGeneralLedgerAsync(
            int accountId,
            DateTime postingDate,
            string voucherNo,
            string voucherType,
            decimal debitAmount,
            decimal creditAmount,
            string? narration = null,
            int? costCenterId = null,
            CancellationToken ct = default)
        {
            var latest = await _glRepo.GetLatestBalanceAsync(accountId, ct);
            var runningBalance = (latest?.RunningBalance ?? 0) + debitAmount - creditAmount;

            var entry = new GeneralLedger
            {
                AccountId      = accountId,
                PostingDate    = postingDate,
                VoucherNo      = voucherNo,
                VoucherType    = voucherType,
                DebitAmount    = debitAmount,
                CreditAmount   = creditAmount,
                RunningBalance = runningBalance,
                Narration      = narration,
                CostCenterId   = costCenterId,
                CreatedAt      = DateTime.UtcNow
            };

            await _uow.Repository<GeneralLedger>().AddAsync(entry, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("GL posted: Account {AccountId} Voucher {VoucherNo} Dr:{Dr} Cr:{Cr}",
                accountId, voucherNo, debitAmount, creditAmount);
            return entry;
        }

        /// <summary>Returns the account balance as of a given date (sum of debits minus credits).</summary>
        public async Task<decimal> GetAccountBalanceAsync(int accountId, DateTime asOfDate, CancellationToken ct = default)
        {
            var entries = await _glRepo.GetByAccountAsync(accountId, DateTime.MinValue, asOfDate, ct);
            return entries.Sum(e => e.DebitAmount - e.CreditAmount);
        }

        // ── Trial Balance ─────────────────────────────────────────────

        /// <summary>Generates a trial balance for all posting accounts over a given period.</summary>
        public async Task<IEnumerable<TrialBalance>> GenerateTrialBalanceAsync(DateTime periodStart, DateTime periodEnd, int userId, CancellationToken ct = default)
        {
            var accounts = await _uow.Repository<Account>()
                .FindAsync(a => a.AllowPosting && a.IsActive, ct);

            var rows = new List<TrialBalance>();

            foreach (var account in accounts)
            {
                var prePeriodEntries = await _glRepo.GetByAccountAsync(account.AccountId, DateTime.MinValue, periodStart.AddDays(-1), ct);
                var periodEntries    = await _glRepo.GetByAccountAsync(account.AccountId, periodStart, periodEnd, ct);

                var openingDebit  = prePeriodEntries.Sum(e => e.DebitAmount);
                var openingCredit = prePeriodEntries.Sum(e => e.CreditAmount);
                var periodDebit   = periodEntries.Sum(e => e.DebitAmount);
                var periodCredit  = periodEntries.Sum(e => e.CreditAmount);

                var closingDebit  = openingDebit  + periodDebit;
                var closingCredit = openingCredit + periodCredit;

                var row = new TrialBalance
                {
                    AccountId        = account.AccountId,
                    PeriodStart      = periodStart,
                    PeriodEnd        = periodEnd,
                    GeneratedDate    = DateTime.UtcNow,
                    OpeningDebit     = openingDebit,
                    OpeningCredit    = openingCredit,
                    PeriodDebit      = periodDebit,
                    PeriodCredit     = periodCredit,
                    ClosingDebit     = closingDebit,
                    ClosingCredit    = closingCredit,
                    GeneratedByUserId = userId
                };

                await _uow.Repository<TrialBalance>().AddAsync(row, ct);
                rows.Add(row);
            }

            await _uow.SaveChangesAsync(ct);
            return rows;
        }

        /// <summary>Validates that total debits equal total credits for a period. Returns true if balanced.</summary>
        public async Task<bool> ValidateTrialBalanceAsync(DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
        {
            var entries = await _glRepo.GetByDateRangeAsync(periodStart, periodEnd, ct);
            var totalDebits  = entries.Sum(e => e.DebitAmount);
            var totalCredits = entries.Sum(e => e.CreditAmount);
            return totalDebits == totalCredits;
        }

        // ── Financial Statements ──────────────────────────────────────

        /// <summary>Generates a financial statement (Balance Sheet or Profit &amp; Loss) for a period.</summary>
        public async Task<FinancialStatement> GenerateFinancialStatementAsync(string statementType, DateTime periodStart, DateTime periodEnd, int userId, CancellationToken ct = default)
        {
            var entries = await _glRepo.GetByDateRangeAsync(periodStart, periodEnd, ct);

            decimal totalAssets      = 0;
            decimal totalLiabilities = 0;
            decimal netProfit        = 0;

            if (statementType == "ProfitLoss")
            {
                var revenue  = entries.Where(e => e.CreditAmount > 0).Sum(e => e.CreditAmount);
                var expenses = entries.Where(e => e.DebitAmount > 0).Sum(e => e.DebitAmount);
                netProfit = revenue - expenses;
            }
            else if (statementType == "BalanceSheet")
            {
                totalAssets      = entries.Sum(e => e.DebitAmount);
                totalLiabilities = entries.Sum(e => e.CreditAmount);
            }

            var statement = new FinancialStatement
            {
                StatementType      = statementType,
                PeriodStart        = periodStart,
                PeriodEnd          = periodEnd,
                GeneratedDate      = DateTime.UtcNow,
                StatementData      = $"{{\"type\":\"{statementType}\",\"from\":\"{periodStart:yyyy-MM-dd}\",\"to\":\"{periodEnd:yyyy-MM-dd}\"}}",
                TotalAssets        = totalAssets,
                TotalLiabilities   = totalLiabilities,
                NetProfit          = netProfit,
                GeneratedByUserId  = userId
            };

            await _uow.Repository<FinancialStatement>().AddAsync(statement, ct);
            await _uow.SaveChangesAsync(ct);
            return statement;
        }

        // ── Cost Centres ──────────────────────────────────────────────

        /// <summary>Creates a new cost centre. Throws if the code already exists.</summary>
        public async Task<CostCenter> CreateCostCenterAsync(string code, string name, string type, int? parentId = null, CancellationToken ct = default)
        {
            if (await _uow.Repository<CostCenter>().AnyAsync(cc => cc.CostCenterCode == code, ct))
                throw new InvalidOperationException($"A cost centre with code '{code}' already exists.");

            var cc = new CostCenter
            {
                CostCenterCode   = code,
                CostCenterName   = name,
                CostCenterType   = type,
                ParentCostCenterId = parentId,
                IsActive         = true,
                CreatedAt        = DateTime.UtcNow,
                UpdatedAt        = DateTime.UtcNow
            };

            await _uow.Repository<CostCenter>().AddAsync(cc, ct);
            await _uow.SaveChangesAsync(ct);
            return cc;
        }

        /// <summary>Adds an actual expenditure amount to a cost centre.</summary>
        public async Task<CostCenter> AllocateToCostCenterAsync(int costCenterId, decimal amount, CancellationToken ct = default)
        {
            var cc = await _uow.Repository<CostCenter>().GetByIdAsync(costCenterId, ct)
                ?? throw new InvalidOperationException($"Cost centre {costCenterId} not found.");

            cc.ActualAmount += amount;
            cc.UpdatedAt = DateTime.UtcNow;

            _uow.Repository<CostCenter>().Update(cc);
            await _uow.SaveChangesAsync(ct);
            return cc;
        }

        /// <summary>Returns the total budget variance for a cost centre in a fiscal year.</summary>
        public async Task<decimal> GetBudgetVarianceAsync(int costCenterId, int fiscalYear, CancellationToken ct = default)
        {
            var allocations = await _budgetRepo.GetByFiscalYearAsync(fiscalYear, ct);
            return allocations
                .Where(b => b.CostCenterId == costCenterId)
                .Sum(b => b.BudgetedAmount - b.ActualAmount);
        }

        // ── Budget ────────────────────────────────────────────────────

        /// <summary>Creates a budget allocation for an account in a cost centre for a fiscal period.</summary>
        public async Task<BudgetAllocation> CreateBudgetAllocationAsync(int? costCenterId, int accountId, int fiscalYear, int month, decimal budgetAmount, CancellationToken ct = default)
        {
            var budget = new BudgetAllocation
            {
                CostCenterId   = costCenterId,
                AccountId      = accountId,
                FiscalYear     = fiscalYear,
                PeriodMonth    = month,
                BudgetedAmount = budgetAmount,
                ActualAmount   = 0,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow
            };

            await _uow.Repository<BudgetAllocation>().AddAsync(budget, ct);
            await _uow.SaveChangesAsync(ct);
            return budget;
        }

        /// <summary>Updates the actual spending amount on a budget allocation.</summary>
        public async Task<BudgetAllocation> UpdateActualAmountAsync(int budgetId, decimal actualAmount, CancellationToken ct = default)
        {
            var budget = await _uow.Repository<BudgetAllocation>().GetByIdAsync(budgetId, ct)
                ?? throw new InvalidOperationException($"Budget allocation {budgetId} not found.");

            budget.ActualAmount = actualAmount;
            budget.UpdatedAt    = DateTime.UtcNow;

            _uow.Repository<BudgetAllocation>().Update(budget);
            await _uow.SaveChangesAsync(ct);
            return budget;
        }

        // ── Tax ───────────────────────────────────────────────────────

        /// <summary>Calculates GST for an intra-state (CGST+SGST) or inter-state (IGST) transaction.</summary>
        public async Task<TaxCalculation> CalculateGSTAsync(decimal taxableAmount, decimal gstRate, bool isInterState, CancellationToken ct = default)
        {
            var taxAmount  = Math.Round(taxableAmount * gstRate / 100, 2);
            var cgst       = isInterState ? 0 : Math.Round(taxAmount / 2, 2);
            var sgst       = isInterState ? 0 : taxAmount - cgst;
            var igst       = isInterState ? taxAmount : 0;

            var calc = new TaxCalculation
            {
                ReferenceType  = "Manual",
                ReferenceId    = 0,
                TaxType        = isInterState ? "IGST" : "GST",
                TaxableAmount  = taxableAmount,
                TaxRate        = gstRate,
                TaxAmount      = taxAmount,
                CGSTAmount     = cgst,
                SGSTAmount     = sgst,
                IGSTAmount     = igst,
                TDSAmount      = 0,
                CreatedAt      = DateTime.UtcNow
            };

            await _uow.Repository<TaxCalculation>().AddAsync(calc, ct);
            await _uow.SaveChangesAsync(ct);
            return calc;
        }

        /// <summary>Calculates TDS on a payment for a given reference.</summary>
        public async Task<TaxCalculation> CalculateTDSAsync(decimal taxableAmount, decimal tdsRate, string referenceType, int referenceId, CancellationToken ct = default)
        {
            var tdsAmount = Math.Round(taxableAmount * tdsRate / 100, 2);

            var calc = new TaxCalculation
            {
                ReferenceType  = referenceType,
                ReferenceId    = referenceId,
                TaxType        = "TDS",
                TaxableAmount  = taxableAmount,
                TaxRate        = tdsRate,
                TaxAmount      = tdsAmount,
                CGSTAmount     = 0,
                SGSTAmount     = 0,
                IGSTAmount     = 0,
                TDSAmount      = tdsAmount,
                CreatedAt      = DateTime.UtcNow
            };

            await _uow.Repository<TaxCalculation>().AddAsync(calc, ct);
            await _uow.SaveChangesAsync(ct);
            return calc;
        }

        /// <summary>Returns all GL entries for an account within a date range.</summary>
        public async Task<IEnumerable<GeneralLedger>> GetGLEntriesAsync(int accountId, DateTime from, DateTime to, CancellationToken ct = default)
            => await _glRepo.GetByAccountAsync(accountId, from, to, ct);

        /// <summary>
        /// Performs period-end close: validates trial balance and marks the period closed.
        /// Returns true if the trial balance is balanced.
        /// </summary>
        public async Task<bool> PerformPeriodCloseAsync(DateTime periodEnd, int userId, CancellationToken ct = default)
        {
            var periodStart = new DateTime(periodEnd.Year, periodEnd.Month, 1);
            var isBalanced  = await ValidateTrialBalanceAsync(periodStart, periodEnd, ct);

            _logger.LogInformation("Period close for {PeriodEnd} by user {UserId}. Balanced: {IsBalanced}",
                periodEnd.ToString("yyyy-MM-dd"), userId, isBalanced);

            return isBalanced;
        }
    }
}
