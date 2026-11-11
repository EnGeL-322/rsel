using System.Windows;
using System.Windows.Controls;
using Comercium.App.ViewModels;

namespace Comercium.App.Views;

public partial class AccountView : UserControl
{
    public AccountView() => InitializeComponent();

    private void Change(object sender, RoutedEventArgs e)
    {
        if (DataContext is AccountModel model)
            model.Change(Current.Password, Replacement.Password, Confirmation.Password);
        Current.Clear();
        Replacement.Clear();
        Confirmation.Clear();
    }
}
