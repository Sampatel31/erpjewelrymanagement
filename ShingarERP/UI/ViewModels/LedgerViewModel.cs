using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Constants;
using ShingarERP.Core.Models;
using ShingarERP.Services;

namespace ShingarERP.UI.ViewModels
{
    /// <summary>
    /// ViewModel for Chart of Accounts and Journal Ledger (Module 22).
    /// </summary>
    public partial class LedgerViewModel : ObservableObject
    {
        private readonly AccountingService _accountingService;
        private readonly ILogger<LedgerViewModel> _logger;

        // ── Observable Properties ────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<Account> _accounts = new();

        [ObservableProperty]
        private ObservableCollection<JournalEntryDto> _journalEntries = new();

        [ObservableProperty]
        private ObservableCollection<TrialBalanceRow> _trialBalance = new();

        [ObservableProperty]
        private Account? _selectedAccount;

        [ObservableProperty]
        private JournalEntryDto? _selectedEntry;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private DateTime _fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        [ObservableProperty]
        private DateTime _toDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _trialBalanceAsOf = DateTime.Today;

        [ObservableProperty]
        private string _activeTab = "Accounts";

        [ObservableProperty]
        private CreateJournalEntryRequest _newEntryRequest = new()
        {
            VoucherType = AppConstants.VoucherType.Journal,
            VoucherDate = DateTime.Today
        };

        // Line being built for the new entry
        [ObservableProperty]
        private ObservableCollection<CreateJournalLineRequest> _newEntryLines = new();

        // ── Constructor ──────────────────────────────────────────────

        public LedgerViewModel(AccountingService accountingService, ILogger<LedgerViewModel> logger)
        {
            _accountingService = accountingService;
            _logger            = logger;
        }

        // ── Commands ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task LoadAccountsAsync(CancellationToken ct = default)
        {
            IsBusy = true;
            StatusMessage = "Loading accounts…";
            try
            {
                Accounts.Clear();
                var accounts = await _accountingService.GetAccountsAsync(null, ct);
                foreach (var a in accounts)
                    Accounts.Add(a);

                StatusMessage = $"{Accounts.Count} accounts loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to load accounts");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PostJournalEntryAsync(CancellationToken ct = default)
        {
            if (NewEntryLines.Count < 2)
            {
                StatusMessage = "A journal entry requires at least 2 lines.";
                return;
            }

            IsBusy = true;
            try
            {
                NewEntryRequest.Lines.Clear();
                foreach (var line in NewEntryLines)
                    NewEntryRequest.Lines.Add(line);

                var entry = await _accountingService.PostJournalEntryAsync(NewEntryRequest, ct);
                JournalEntries.Insert(0, entry);
                StatusMessage     = $"Voucher '{entry.VoucherNo}' posted. Debit: ₹{entry.TotalDebit:F2}";

                // Reset form
                NewEntryRequest = new CreateJournalEntryRequest
                {
                    VoucherType = AppConstants.VoucherType.Journal,
                    VoucherDate = DateTime.Today
                };
                NewEntryLines.Clear();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to post journal entry");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddEntryLine()
        {
            NewEntryLines.Add(new CreateJournalLineRequest());
        }

        [RelayCommand]
        private void RemoveEntryLine(CreateJournalLineRequest line)
        {
            NewEntryLines.Remove(line);
        }

        [RelayCommand]
        private async Task LoadTrialBalanceAsync(CancellationToken ct = default)
        {
            IsBusy        = true;
            StatusMessage = "Generating trial balance…";
            try
            {
                TrialBalance.Clear();
                var rows = await _accountingService.GetTrialBalanceAsync(TrialBalanceAsOf, ct);
                foreach (var row in rows)
                    TrialBalance.Add(row);

                StatusMessage = $"Trial balance generated as of {TrialBalanceAsOf:dd-MM-yyyy}. {rows.Count} accounts.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to generate trial balance");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ExportTallyXmlAsync(CancellationToken ct = default)
        {
            IsBusy = true;
            try
            {
                var xml     = await _accountingService.ExportToTallyXmlAsync(FromDate, ToDate, ct);
                var path    = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"ShingarTally_{DateTime.Now:yyyyMMddHHmmss}.xml");

                await System.IO.File.WriteAllTextAsync(path, xml, System.Text.Encoding.UTF8, ct);
                StatusMessage = $"Tally XML exported to: {path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export error: {ex.Message}";
                _logger.LogError(ex, "Failed to export Tally XML");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CreateAccountAsync(Account account, CancellationToken ct = default)
        {
            IsBusy = true;
            try
            {
                var created = await _accountingService.CreateAccountAsync(account, ct);
                Accounts.Add(created);
                StatusMessage = $"Account '{created.AccountCode} – {created.AccountName}' created.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to create account");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
