using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ShingarERP.UI.ViewModels;
using ShingarERP.UI.Views.Accounting;
using ShingarERP.UI.Views.Customer;
using ShingarERP.UI.Views.Inventory;

namespace ShingarERP
{
    /// <summary>
    /// Main application window – navigation shell.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MetalInventoryViewModel  _metalVM;
        private readonly FinishedGoodsViewModel   _finishedVM;
        private readonly CustomerMasterViewModel  _customerVM;
        private readonly LedgerViewModel          _ledgerVM;

        public MainWindow(
            MetalInventoryViewModel  metalVM,
            FinishedGoodsViewModel   finishedVM,
            CustomerMasterViewModel  customerVM,
            LedgerViewModel          ledgerVM)
        {
            _metalVM    = metalVM;
            _finishedVM = finishedVM;
            _customerVM = customerVM;
            _ledgerVM   = ledgerVM;

            InitializeComponent();

            // Load Metal Inventory as default view
            NavigateTo("MetalInventory");
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
                NavigateTo(tag);
        }

        private void NavigateTo(string view)
        {
            UserControl? content = view switch
            {
                "MetalInventory" => new MetalInventoryView  { DataContext = _metalVM },
                "FinishedGoods"  => new FinishedGoodsView   { DataContext = _finishedVM },
                "CustomerMaster" => new CustomerMasterView  { DataContext = _customerVM },
                "Ledger"         => new LedgerView          { DataContext = _ledgerVM },
                "AccountChart"   => new AccountChartView    { DataContext = _ledgerVM },
                _                => null
            };

            if (content != null)
                ContentFrame.Content = content;
        }
    }
}
