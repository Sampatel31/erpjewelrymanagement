using System.Windows.Controls;
using System.Windows.Input;

namespace ShingarERP.UI.Views.Customer
{
    public partial class CustomerMasterView : UserControl
    {
        public CustomerMasterView()
        {
            InitializeComponent();
        }

        private void DataGridRow_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ShingarERP.UI.ViewModels.CustomerMasterViewModel vm)
                vm.EditCustomerCommand.Execute(null);
        }
    }
}
