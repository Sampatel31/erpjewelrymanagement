using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.DTOs;
using ShingarERP.Services;

namespace ShingarERP.UI.ViewModels
{
    /// <summary>
    /// ViewModel for Metal Inventory master screen (Module 01).
    /// Displays metal lots, current gold rates, and stock alerts.
    /// </summary>
    public partial class MetalInventoryViewModel : ObservableObject
    {
        private readonly InventoryService _inventoryService;
        private readonly GoldRateService  _goldRateService;
        private readonly ILogger<MetalInventoryViewModel> _logger;

        // ── Observable Properties ────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<MetalDto> _metals = new();

        [ObservableProperty]
        private ObservableCollection<MetalLotDto> _metalLots = new();

        [ObservableProperty]
        private MetalDto? _selectedMetal;

        [ObservableProperty]
        private MetalRateDto? _currentRate;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _hasLowStockAlert;

        // New lot form
        [ObservableProperty]
        private CreateMetalLotRequest _newLotRequest = new()
        {
            LotNumber  = string.Empty,
            PurchaseDate = DateTime.Today
        };

        // New rate form
        [ObservableProperty]
        private CreateMetalRateRequest _newRateRequest = new()
        {
            RateDate = DateTime.Today,
            Source   = "Manual"
        };

        // ── Constructor ──────────────────────────────────────────────

        public MetalInventoryViewModel(
            InventoryService inventoryService,
            GoldRateService  goldRateService,
            ILogger<MetalInventoryViewModel> logger)
        {
            _inventoryService = inventoryService;
            _goldRateService  = goldRateService;
            _logger           = logger;
        }

        // ── Commands ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task LoadMetalsAsync(CancellationToken ct = default)
        {
            IsBusy = true;
            StatusMessage = "Loading metals…";
            try
            {
                Metals.Clear();
                var metals = await _inventoryService.GetAllMetalsAsync(ct);
                foreach (var m in metals)
                    Metals.Add(m);

                // Load stock summary and check alerts
                var summary = await _inventoryService.GetMetalStockSummaryAsync(ct);
                HasLowStockAlert = summary.Exists(s => s.BelowMinimum);

                StatusMessage = $"{Metals.Count} metals loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to load metals");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task LoadRateAsync(CancellationToken ct = default)
        {
            if (SelectedMetal == null) return;

            IsBusy = true;
            try
            {
                CurrentRate = await _goldRateService.GetCurrentRateAsync(SelectedMetal.MetalId, ct);
                if (CurrentRate == null)
                    StatusMessage = "No rate available for selected metal. Please enter today's rate.";
                else
                    StatusMessage = $"Current rate: ₹{CurrentRate.RatePerGram:F2}/g ({CurrentRate.Source})";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error fetching rate: {ex.Message}";
                _logger.LogError(ex, "Failed to load rate for metal {MetalId}", SelectedMetal.MetalId);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveMetalRateAsync(CancellationToken ct = default)
        {
            if (NewRateRequest.MetalId <= 0)
            {
                StatusMessage = "Please select a metal before saving rate.";
                return;
            }

            IsBusy = true;
            try
            {
                CurrentRate   = await _goldRateService.SaveManualRateAsync(NewRateRequest, ct);
                StatusMessage = $"Rate saved: ₹{CurrentRate.RatePerGram:F2}/g";
                NewRateRequest = new CreateMetalRateRequest { RateDate = DateTime.Today, Source = "Manual" };
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving rate: {ex.Message}";
                _logger.LogError(ex, "Failed to save metal rate");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CreateMetalLotAsync(CancellationToken ct = default)
        {
            IsBusy = true;
            try
            {
                var lot = await _inventoryService.CreateMetalLotAsync(NewLotRequest, ct);
                MetalLots.Insert(0, lot);
                StatusMessage  = $"Lot '{lot.LotNumber}' created successfully.";
                NewLotRequest  = new CreateMetalLotRequest { LotNumber = string.Empty, PurchaseDate = DateTime.Today };
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to create metal lot");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSelectedMetalChanged(MetalDto? value)
        {
            if (value != null)
            {
                NewLotRequest.MetalId  = value.MetalId;
                NewRateRequest.MetalId = value.MetalId;
                // Trigger rate load
                _ = LoadRateAsync();
            }
        }
    }
}
