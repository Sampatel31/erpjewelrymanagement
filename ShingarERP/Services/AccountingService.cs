using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Constants;
using ShingarERP.Core.Interfaces;
using ShingarERP.Core.Models;
using ShingarERP.Data;

namespace ShingarERP.Services
{
    // ── DTOs used by AccountingService ──────────────────────────────

    public class JournalEntryDto
    {
        public int      EntryId      { get; set; }
        public string   VoucherNo    { get; set; } = string.Empty;
        public string   VoucherType  { get; set; } = string.Empty;
        public DateTime VoucherDate  { get; set; }
        public string?  Narration    { get; set; }
        public decimal  TotalDebit   { get; set; }
        public decimal  TotalCredit  { get; set; }
        public bool     IsPosted     { get; set; }
        public List<JournalLineDto> Lines { get; set; } = new();
    }

    public class JournalLineDto
    {
        public int     AccountId    { get; set; }
        public string  AccountName  { get; set; } = string.Empty;
        public decimal DebitAmount  { get; set; }
        public decimal CreditAmount { get; set; }
        public string? Narration    { get; set; }
    }

    public class CreateJournalEntryRequest
    {
        public string VoucherType  { get; set; } = AppConstants.VoucherType.Journal;
        public DateTime VoucherDate { get; set; }
        public string? Narration   { get; set; }
        public string? ReferenceNo { get; set; }
        public List<CreateJournalLineRequest> Lines { get; set; } = new();
    }

    public class CreateJournalLineRequest
    {
        public int     AccountId    { get; set; }
        public decimal DebitAmount  { get; set; }
        public decimal CreditAmount { get; set; }
        public string? Narration    { get; set; }
    }

    public class TrialBalanceRow
    {
        public string   AccountCode    { get; set; } = string.Empty;
        public string   AccountName    { get; set; } = string.Empty;
        public string   AccountType    { get; set; } = string.Empty;
        public decimal  TotalDebit     { get; set; }
        public decimal  TotalCredit    { get; set; }
        public decimal  NetBalance     { get; set; }
    }

    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Double-entry accounting service (Module 22).
    /// Handles chart of accounts, journal entries, and reports.
    /// </summary>
    public class AccountingService
    {
        private readonly IUnitOfWork _uow;
        private readonly ShingarContext _context;
        private readonly ILogger<AccountingService> _logger;

        private int _voucherSequence = 0;
        private readonly object _seqLock = new();

        public AccountingService(IUnitOfWork uow, ShingarContext context, ILogger<AccountingService> logger)
        {
            _uow     = uow;
            _context = context;
            _logger  = logger;
        }

        // ── Chart of Accounts ────────────────────────────────────────

        /// <summary>Get all accounts with optional type filter.</summary>
        public async Task<List<Account>> GetAccountsAsync(string? accountType = null, CancellationToken ct = default)
        {
            var query = _context.Accounts.AsNoTracking().Where(a => a.IsActive);

            if (!string.IsNullOrWhiteSpace(accountType))
                query = query.Where(a => a.AccountType == accountType);

            return await query.OrderBy(a => a.AccountCode).ToListAsync(ct);
        }

        /// <summary>Create a new account in the COA.</summary>
        public async Task<Account> CreateAccountAsync(Account account, CancellationToken ct = default)
        {
            if (await _uow.Repository<Account>().AnyAsync(a => a.AccountCode == account.AccountCode, ct))
                throw new InvalidOperationException($"Account code '{account.AccountCode}' already exists.");

            await _uow.Repository<Account>().AddAsync(account, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("Account created: {Code} – {Name}", account.AccountCode, account.AccountName);
            return account;
        }

        // ── Journal Entries ──────────────────────────────────────────

        /// <summary>
        /// Post a double-entry journal voucher.
        /// Validates that TotalDebit == TotalCredit (balanced entry).
        /// </summary>
        public async Task<JournalEntryDto> PostJournalEntryAsync(CreateJournalEntryRequest request, CancellationToken ct = default)
        {
            if (request.Lines == null || request.Lines.Count < 2)
                throw new ArgumentException("A journal entry must have at least 2 lines.");

            var totalDebit  = request.Lines.Sum(l => l.DebitAmount);
            var totalCredit = request.Lines.Sum(l => l.CreditAmount);

            if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                throw new InvalidOperationException(
                    $"Journal entry is not balanced. Debit: {totalDebit:F2}, Credit: {totalCredit:F2}");

            await _uow.BeginTransactionAsync(ct);
            try
            {
                var entry = new JournalEntry
                {
                    VoucherNo   = GenerateVoucherNo(request.VoucherType, request.VoucherDate),
                    VoucherType = request.VoucherType,
                    VoucherDate = request.VoucherDate,
                    Narration   = request.Narration,
                    ReferenceNo = request.ReferenceNo,
                    TotalDebit  = totalDebit,
                    TotalCredit = totalCredit,
                    IsPosted    = true
                };

                await _uow.Repository<JournalEntry>().AddAsync(entry, ct);
                await _uow.SaveChangesAsync(ct); // Get EntryId

                int sortOrder = 1;
                foreach (var lineReq in request.Lines)
                {
                    var account = await _uow.Repository<Account>().GetByIdAsync(lineReq.AccountId, ct)
                        ?? throw new InvalidOperationException($"Account {lineReq.AccountId} not found.");

                    if (!account.AllowPosting)
                        throw new InvalidOperationException($"Account '{account.AccountName}' is a control account and does not allow posting.");

                    var line = new JournalEntryLine
                    {
                        EntryId      = entry.EntryId,
                        AccountId    = lineReq.AccountId,
                        DebitAmount  = lineReq.DebitAmount,
                        CreditAmount = lineReq.CreditAmount,
                        Narration    = lineReq.Narration,
                        SortOrder    = sortOrder++
                    };

                    await _uow.Repository<JournalEntryLine>().AddAsync(line, ct);

                    // Update account balance
                    var netImpact = lineReq.DebitAmount - lineReq.CreditAmount;
                    if (account.NormalBalance == "Dr")
                        account.CurrentBalance += netImpact;
                    else
                        account.CurrentBalance -= netImpact;

                    account.UpdatedAt = DateTime.UtcNow;
                    _uow.Repository<Account>().Update(account);
                }

                await _uow.SaveChangesAsync(ct);
                await _uow.CommitTransactionAsync(ct);

                _logger.LogInformation("Journal entry posted: {VoucherNo}, Debit: {Debit:F2}", entry.VoucherNo, totalDebit);

                return await GetJournalEntryDtoAsync(entry.EntryId, ct);
            }
            catch
            {
                await _uow.RollbackTransactionAsync(ct);
                throw;
            }
        }

        // ── Reports ──────────────────────────────────────────────────

        /// <summary>Generate trial balance as of a given date.</summary>
        public async Task<List<TrialBalanceRow>> GetTrialBalanceAsync(DateTime asOfDate, CancellationToken ct = default)
        {
            var accounts = await _context.Accounts
                .AsNoTracking()
                .Include(a => a.EntryLines)
                    .ThenInclude(l => l.JournalEntry)
                .Where(a => a.IsActive && a.AllowPosting)
                .ToListAsync(ct);

            var rows = new List<TrialBalanceRow>();

            foreach (var account in accounts)
            {
                var linesUpToDate = account.EntryLines
                    .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.VoucherDate <= asOfDate)
                    .ToList();

                var totalDebit  = linesUpToDate.Sum(l => l.DebitAmount);
                var totalCredit = linesUpToDate.Sum(l => l.CreditAmount);
                var netBalance  = account.OpeningBalance + totalDebit - totalCredit;

                rows.Add(new TrialBalanceRow
                {
                    AccountCode = account.AccountCode,
                    AccountName = account.AccountName,
                    AccountType = account.AccountType,
                    TotalDebit  = totalDebit,
                    TotalCredit = totalCredit,
                    NetBalance  = netBalance
                });
            }

            return rows.OrderBy(r => r.AccountCode).ToList();
        }

        /// <summary>
        /// Generate day book entries for a given date and book type.
        /// </summary>
        public async Task<DayBook> GetOrCreateDayBookAsync(DateTime date, string bookType, int accountId, CancellationToken ct = default)
        {
            var existing = await _context.DayBooks
                .FirstOrDefaultAsync(d => d.BookDate == date.Date &&
                                          d.BookType  == bookType  &&
                                          d.AccountId == accountId, ct);

            if (existing != null) return existing;

            // Compute previous closing balance
            var prevDay = await _context.DayBooks
                .AsNoTracking()
                .Where(d => d.BookDate < date.Date && d.BookType == bookType && d.AccountId == accountId)
                .OrderByDescending(d => d.BookDate)
                .FirstOrDefaultAsync(ct);

            var account = await _uow.Repository<Account>().GetByIdAsync(accountId, ct)
                ?? throw new InvalidOperationException($"Account {accountId} not found.");

            var dayBook = new DayBook
            {
                BookDate       = date.Date,
                BookType       = bookType,
                AccountId      = accountId,
                OpeningBalance = prevDay?.ClosingBalance ?? account.OpeningBalance,
                TotalReceipts  = 0,
                TotalPayments  = 0,
                ClosingBalance = prevDay?.ClosingBalance ?? account.OpeningBalance,
                IsClosed       = false
            };

            await _uow.Repository<DayBook>().AddAsync(dayBook, ct);
            await _uow.SaveChangesAsync(ct);

            return dayBook;
        }

        // ── Tally XML Export ─────────────────────────────────────────

        /// <summary>Generate a basic Tally XML export for journal entries.</summary>
        public async Task<string> ExportToTallyXmlAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        {
            var entries = await _context.JournalEntries
                .AsNoTracking()
                .Include(e => e.Lines)
                    .ThenInclude(l => l.Account)
                .Where(e => e.IsPosted && !e.IsReversed &&
                             e.VoucherDate >= fromDate && e.VoucherDate <= toDate)
                .OrderBy(e => e.VoucherDate)
                .ToListAsync(ct);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<ENVELOPE>");
            sb.AppendLine("<HEADER><TALLYREQUEST>Import Data</TALLYREQUEST></HEADER>");
            sb.AppendLine("<BODY><IMPORTDATA><REQUESTDESC><REPORTNAME>Vouchers</REPORTNAME></REQUESTDESC>");
            sb.AppendLine("<REQUESTDATA>");

            foreach (var entry in entries)
            {
                sb.AppendLine($"<TALLYMESSAGE xmlns:UDF=\"TallyUDF\">");
                sb.AppendLine($"<VOUCHER VCHTYPE=\"{entry.VoucherType}\" ACTION=\"Create\">");
                sb.AppendLine($"<DATE>{entry.VoucherDate:yyyyMMdd}</DATE>");
                sb.AppendLine($"<VOUCHERNUMBER>{System.Security.SecurityElement.Escape(entry.VoucherNo)}</VOUCHERNUMBER>");
                sb.AppendLine($"<NARRATION>{System.Security.SecurityElement.Escape(entry.Narration ?? string.Empty)}</NARRATION>");

                foreach (var line in entry.Lines.OrderBy(l => l.SortOrder))
                {
                    if (line.DebitAmount > 0)
                    {
                        sb.AppendLine($"<ALLLEDGERENTRIES.LIST>");
                        sb.AppendLine($"<LEDGERNAME>{System.Security.SecurityElement.Escape(line.Account.AccountName)}</LEDGERNAME>");
                        sb.AppendLine($"<ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE>");
                        sb.AppendLine($"<AMOUNT>-{line.DebitAmount:F2}</AMOUNT>");
                        sb.AppendLine($"</ALLLEDGERENTRIES.LIST>");
                    }
                    if (line.CreditAmount > 0)
                    {
                        sb.AppendLine($"<ALLLEDGERENTRIES.LIST>");
                        sb.AppendLine($"<LEDGERNAME>{System.Security.SecurityElement.Escape(line.Account.AccountName)}</LEDGERNAME>");
                        sb.AppendLine($"<ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE>");
                        sb.AppendLine($"<AMOUNT>{line.CreditAmount:F2}</AMOUNT>");
                        sb.AppendLine($"</ALLLEDGERENTRIES.LIST>");
                    }
                }

                sb.AppendLine("</VOUCHER></TALLYMESSAGE>");
            }

            sb.AppendLine("</REQUESTDATA></IMPORTDATA></BODY></ENVELOPE>");
            return sb.ToString();
        }

        // ── Private helpers ──────────────────────────────────────────

        private string GenerateVoucherNo(string type, DateTime date)
        {
            lock (_seqLock)
            {
                _voucherSequence++;
                return $"{type.Substring(0, Math.Min(3, type.Length)).ToUpper()}-{date:yyyyMM}-{_voucherSequence:D4}";
            }
        }

        private async Task<JournalEntryDto> GetJournalEntryDtoAsync(int entryId, CancellationToken ct)
        {
            var entry = await _context.JournalEntries
                .AsNoTracking()
                .Include(e => e.Lines)
                    .ThenInclude(l => l.Account)
                .FirstOrDefaultAsync(e => e.EntryId == entryId, ct)
                ?? throw new InvalidOperationException($"Entry {entryId} not found after saving.");

            return new JournalEntryDto
            {
                EntryId     = entry.EntryId,
                VoucherNo   = entry.VoucherNo,
                VoucherType = entry.VoucherType,
                VoucherDate = entry.VoucherDate,
                Narration   = entry.Narration,
                TotalDebit  = entry.TotalDebit,
                TotalCredit = entry.TotalCredit,
                IsPosted    = entry.IsPosted,
                Lines       = entry.Lines.OrderBy(l => l.SortOrder).Select(l => new JournalLineDto
                {
                    AccountId    = l.AccountId,
                    AccountName  = l.Account.AccountName,
                    DebitAmount  = l.DebitAmount,
                    CreditAmount = l.CreditAmount,
                    Narration    = l.Narration
                }).ToList()
            };
        }
    }
}
