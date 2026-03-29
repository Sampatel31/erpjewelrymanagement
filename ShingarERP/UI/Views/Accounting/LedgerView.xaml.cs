using System.Windows.Controls;
using ShingarERP.Core.Models;

namespace ShingarERP.UI.Views.Accounting
{
    public partial class LedgerView : UserControl
    {
        public LedgerView()
        {
            InitializeComponent();
        }

        private void CreateAccount_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ShingarERP.UI.ViewModels.LedgerViewModel vm)
            {
                var account = new Account
                {
                    AccountCode    = NewAccountCode.Text.Trim(),
                    AccountName    = NewAccountName.Text.Trim(),
                    AccountType    = (NewAccountType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Asset",
                    NormalBalance  = (NewAccountNormal.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Dr",
                    AllowPosting   = true,
                    IsActive       = true
                };
                vm.CreateAccountCommand.Execute(account);
            }
        }
    }
}
