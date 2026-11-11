using System.Windows;
using System.Windows.Controls;
using Comercium.App.ViewModels;

namespace Comercium.App.Views;

public partial class LoginView : UserControl
{
    public LoginView() => InitializeComponent();

    private void Submit(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginModel model)
            model.Submit(Secret.Password, Confirmation.Password);
        Secret.Clear();
        Confirmation.Clear();
    }
}
