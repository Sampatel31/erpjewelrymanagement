using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.Constants;
using ShingarERP.Core.DTOs;
using ShingarERP.Services;

namespace ShingarERP.UI.ViewModels
{
    /// <summary>
    /// ViewModel for Finished Goods SKU management (Module 02).
    /// </summary>
    public partial class FinishedGoodsViewModel : ObservableObject
    {
        private readonly InventoryService _inventoryService;
        private readonly ILogger<FinishedGoodsViewModel> _logger;

        // ── Observable Properties ────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<FinishedGoodDto> _finishedGoods = new();

        [ObservableProperty]
        private FinishedGoodDto? _selectedItem;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedLocation = string.Empty;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _pageSize = 25;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _showAgingItems;

        [ObservableProperty]
        private CreateFinishedGoodRequest _newItemRequest = new()
        {
            SKU           = string.Empty,
            ItemName      = string.Empty,
            StockLocation = AppConstants.StockLocation.Showroom
        };

        // ── Constructor ──────────────────────────────────────────────

        public FinishedGoodsViewModel(
            InventoryService inventoryService,
            ILogger<FinishedGoodsViewModel> logger)
        {
            _inventoryService = inventoryService;
            _logger           = logger;
        }

        // ── Commands ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task LoadItemsAsync(CancellationToken ct = default)
        {
            IsBusy = true;
            StatusMessage = "Loading inventory…";
            try
            {
                FinishedGoods.Clear();
                var location = string.IsNullOrWhiteSpace(SelectedLocation) ? null : SelectedLocation;
                var search   = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;

                var (items, total) = await _inventoryService.GetFinishedGoodsAsync(
                    search, location, null, CurrentPage, PageSize, ct);

                foreach (var item in items)
                    FinishedGoods.Add(item);

                TotalCount    = total;
                StatusMessage = $"{total} items found.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to load finished goods");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SearchAsync(CancellationToken ct = default)
        {
            CurrentPage = 1;
            await LoadItemsAsync(ct);
        }

        [RelayCommand]
        private async Task CreateItemAsync(CancellationToken ct = default)
        {
            IsBusy = true;
            try
            {
                var item = await _inventoryService.CreateFinishedGoodAsync(NewItemRequest, ct);
                FinishedGoods.Insert(0, item);
                TotalCount++;
                StatusMessage  = $"Item '{item.SKU}' created successfully.";
                NewItemRequest = new CreateFinishedGoodRequest
                {
                    SKU           = string.Empty,
                    ItemName      = string.Empty,
                    StockLocation = AppConstants.StockLocation.Showroom
                };
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to create finished good");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task TransferStockAsync(string toLocation, CancellationToken ct = default)
        {
            if (SelectedItem == null)
            {
                StatusMessage = "No item selected.";
                return;
            }

            IsBusy = true;
            try
            {
                var request = new StockAdjustmentRequest
                {
                    ItemId          = SelectedItem.ItemId,
                    AdjustmentType  = AppConstants.TransactionType.Transfer,
                    QuantityChange  = 0,          // Transfer does not change quantity, only location
                    FromLocation    = SelectedItem.StockLocation,
                    ToLocation      = toLocation
                };

                await _inventoryService.TransferStockAsync(request, ct);
                SelectedItem.StockLocation = toLocation;

                StatusMessage = $"Item '{SelectedItem.SKU}' transferred to {toLocation}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to transfer stock");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task LoadAgingStockAsync(CancellationToken ct = default)
        {
            IsBusy        = true;
            ShowAgingItems = true;
            StatusMessage = "Loading aging stock…";
            try
            {
                FinishedGoods.Clear();
                var items = await _inventoryService.GetAgingStockAsync(180, ct);
                foreach (var item in items)
                    FinishedGoods.Add(item);

                TotalCount    = items.Count;
                StatusMessage = $"{items.Count} aging items (180+ days) found.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to load aging stock");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void NextPage()
        {
            CurrentPage++;
            _ = LoadItemsAsync();
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                _ = LoadItemsAsync();
            }
        }
    }
}
