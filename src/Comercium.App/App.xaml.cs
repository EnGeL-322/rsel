using System.Globalization;
using System.IO;
using System.Windows;
using Comercium.App.ViewModels;
using Comercium.Infrastructure.Persistence;
using Comercium.Infrastructure.Security;

namespace Comercium.App;

public partial class App : System.Windows.Application
{
    private FileStream? instanceLock;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var culture = CultureInfo.GetCultureInfo("es-PE");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Comercium");
        try
        {
            Directory.CreateDirectory(folder);
            try
            {
                instanceLock = File.Open(Path.Combine(folder, "comercium.lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
            {
                MessageBox.Show("Comercium ya está abierto con esta carpeta de datos.", "Comercium", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            var database = new StoreDatabase(Path.Combine(folder, "comercium.db"));
            database.Migrate();
            var dialogs = new DesktopDialogs(folder);
            var accounts = new Accounts(database);
            var shell = new ShellModel(accounts, new StoreBackups(database), dialogs, new Catalog(database), new SalesRegister(database),
                new DispatchRegister(database), new DashboardQueries(database), new PurchaseRegister(database),
                new Customers(database), new CashRegister(database), new Reporting(database), new Strategy(database));
            MainWindow = new MainWindow { DataContext = shell };
            MainWindow.Show();
        }
        catch (Exception error)
        {
            DesktopDialogs.Log(folder, error);
            MessageBox.Show("No se pudo abrir Comercium. Comprueba el espacio disponible y los permisos de tu carpeta de datos.",
                "Comercium", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instanceLock?.Dispose();
        base.OnExit(e);
    }
}
