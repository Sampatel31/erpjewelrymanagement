using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ShingarERP.Core.DTOs;
using ShingarERP.Services;

namespace ShingarERP.UI.ViewModels
{
    /// <summary>
    /// ViewModel for Customer Master & KYC screen (Module 15).
    /// </summary>
    public partial class CustomerMasterViewModel : ObservableObject
    {
        private readonly CustomerService _customerService;
        private readonly ILogger<CustomerMasterViewModel> _logger;

        // ── Observable Properties ────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<CustomerDto> _customers = new();

        [ObservableProperty]
        private CustomerDto? _selectedCustomer;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _filterKycVerified;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isEditMode;

        [ObservableProperty]
        private CreateCustomerRequest _customerForm = new()
        {
            FirstName = string.Empty,
            Mobile    = string.Empty
        };

        [ObservableProperty]
        private KycVerificationRequest _kycForm = new()
        {
            DocumentType   = "Aadhaar",
            DocumentNumber = string.Empty
        };

        // ── Constructor ──────────────────────────────────────────────

        public CustomerMasterViewModel(
            CustomerService customerService,
            ILogger<CustomerMasterViewModel> logger)
        {
            _customerService = customerService;
            _logger          = logger;
        }

        // ── Commands ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task SearchCustomersAsync(CancellationToken ct = default)
        {
            IsBusy = true;
            StatusMessage = "Searching…";
            try
            {
                Customers.Clear();
                var request = new CustomerSearchRequest
                {
                    SearchTerm  = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                    KYCVerified = FilterKycVerified ? true : null,
                    IsActive    = true,
                    PageNumber  = CurrentPage,
                    PageSize    = 25
                };

                var result = await _customerService.SearchCustomersAsync(request, ct);

                foreach (var c in result.Items)
                    Customers.Add(c);

                TotalCount    = result.TotalCount;
                StatusMessage = $"{result.TotalCount} customers found.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Customer search failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CreateCustomerAsync(CancellationToken ct = default)
        {
            IsBusy = true;
            try
            {
                var customer = await _customerService.CreateCustomerAsync(CustomerForm, ct);
                Customers.Insert(0, customer);
                TotalCount++;
                StatusMessage = $"Customer '{customer.FullName}' ({customer.CustomerCode}) created.";
                CustomerForm  = new CreateCustomerRequest { FirstName = string.Empty, Mobile = string.Empty };
                IsEditMode    = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to create customer");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task UpdateCustomerAsync(CancellationToken ct = default)
        {
            if (SelectedCustomer == null)
            {
                StatusMessage = "No customer selected.";
                return;
            }

            IsBusy = true;
            try
            {
                var updateReq = new UpdateCustomerRequest
                {
                    CustomerId      = SelectedCustomer.CustomerId,
                    FirstName       = CustomerForm.FirstName,
                    LastName        = CustomerForm.LastName,
                    Mobile          = CustomerForm.Mobile,
                    Email           = CustomerForm.Email,
                    Address         = CustomerForm.Address,
                    City            = CustomerForm.City,
                    State           = CustomerForm.State,
                    PinCode         = CustomerForm.PinCode,
                    DateOfBirth     = CustomerForm.DateOfBirth,
                    AnniversaryDate = CustomerForm.AnniversaryDate,
                    Gender          = CustomerForm.Gender,
                    AadhaarNumber   = CustomerForm.AadhaarNumber,
                    PANNumber       = CustomerForm.PANNumber
                };

                var updated = await _customerService.UpdateCustomerAsync(updateReq, ct);

                // Update in collection
                var idx = IndexOfCustomer(updated.CustomerId);
                if (idx >= 0) Customers[idx] = updated;

                StatusMessage = "Customer updated.";
                IsEditMode    = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError(ex, "Failed to update customer");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task VerifyKycAsync(CancellationToken ct = default)
        {
            if (SelectedCustomer == null)
            {
                StatusMessage = "No customer selected.";
                return;
            }

            KycForm.CustomerId = SelectedCustomer.CustomerId;

            IsBusy = true;
            try
            {
                await _customerService.VerifyKYCAsync(KycForm, ct);

                // Refresh LTV
                await _customerService.RecalculateLTVAsync(SelectedCustomer.CustomerId, ct);

                // Refresh selected customer in list
                var refreshed = await _customerService.GetCustomerByIdAsync(SelectedCustomer.CustomerId, ct);
                if (refreshed != null)
                {
                    var idx = IndexOfCustomer(refreshed.CustomerId);
                    if (idx >= 0) Customers[idx] = refreshed;
                    SelectedCustomer = refreshed;
                }

                StatusMessage = "KYC verified successfully.";
                KycForm        = new KycVerificationRequest { DocumentType = "Aadhaar", DocumentNumber = string.Empty };
            }
            catch (Exception ex)
            {
                StatusMessage = $"KYC Error: {ex.Message}";
                _logger.LogError(ex, "KYC verification failed");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void EditCustomer()
        {
            if (SelectedCustomer == null) return;

            CustomerForm = new CreateCustomerRequest
            {
                FirstName       = SelectedCustomer.FirstName,
                LastName        = SelectedCustomer.LastName,
                Mobile          = SelectedCustomer.Mobile,
                Email           = SelectedCustomer.Email,
                Address         = SelectedCustomer.Address,
                City            = SelectedCustomer.City,
                State           = SelectedCustomer.State,
                PinCode         = SelectedCustomer.PinCode,
                DateOfBirth     = SelectedCustomer.DateOfBirth,
                AnniversaryDate = SelectedCustomer.AnniversaryDate,
                Gender          = SelectedCustomer.Gender,
                AadhaarNumber   = SelectedCustomer.AadhaarNumber,
                PANNumber       = SelectedCustomer.PANNumber
            };

            IsEditMode = true;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            CustomerForm = new CreateCustomerRequest { FirstName = string.Empty, Mobile = string.Empty };
            IsEditMode   = false;
        }

        // ── Helpers ──────────────────────────────────────────────────

        private int IndexOfCustomer(int customerId)
        {
            for (int i = 0; i < Customers.Count; i++)
                if (Customers[i].CustomerId == customerId) return i;
            return -1;
        }
    }
}
