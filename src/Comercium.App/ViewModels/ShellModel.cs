using System.Windows.Input;
using Comercium.Application;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public sealed class ShellModel : ViewModel
{
    private readonly IAccounts accounts;
    private readonly IBackups backups;
    private readonly ICatalog catalog;
    private readonly ISales sales;
    private readonly IDispatch dispatch;
    private readonly IDashboard dashboard;
    private readonly IPurchases purchases;
    private ViewModel current = null!;

    public ShellModel(IAccounts accounts, IBackups backups, DesktopDialogs dialogs, ICatalog catalog, ISales sales, IDispatch dispatch,
        IDashboard dashboard, IPurchases purchases) : base(dialogs)
    {
        this.accounts = accounts;
        this.backups = backups;
        this.catalog = catalog;
        this.sales = sales;
        this.dispatch = dispatch;
        this.dashboard = dashboard;
        this.purchases = purchases;
        SignOut();
    }

    public ViewModel Current { get => current; private set => Set(ref current, value); }

    private void SignOut() => Current = new LoginModel(accounts, Dialogs, SignIn);

    private void SignIn(Session session) => Current = new WorkspaceModel(session, Dialogs, catalog, sales, dispatch,
        dashboard, purchases, accounts, backups, SignOut);
}

public sealed class WorkspaceModel : ViewModel
{
    private ViewModel page = null!;
    private string title = "";

    public WorkspaceModel(Session session, DesktopDialogs dialogs, ICatalog catalog, ISales sales, IDispatch dispatch,
        IDashboard dashboard, IPurchases purchases, IAccounts accounts, IBackups backups, Action signOut) : base(dialogs)
    {
        User = session.Name;
        var salesModel = new SalesModel(session, dialogs, catalog, sales);
        GoSales = Action(() => Open("Ventas", salesModel, salesModel.Refresh), () => Allows(Permission.Sell));
        GoDashboard = Action(() => Open("Resumen de la tienda", new DashboardModel(session, dialogs, dashboard, GoSales)),
            () => Allows(Permission.ViewDashboard));
        GoDispatch = Action(() => Open("Despacho en tienda", new DispatchModel(session, dialogs, dispatch)), () => Allows(Permission.Dispatch));
        GoCatalog = Action(() => Open("Productos", new CatalogModel(session, dialogs, catalog)), () => Allows(Permission.ManageCatalog));
        GoInventory = Action(() => Open("Inventario", new InventoryModel(session, dialogs, catalog)), () => Allows(Permission.AdjustInventory));
        GoPurchases = Action(() => Open("Compras y proveedores", new PurchasesModel(session, dialogs, purchases, catalog)), () => Allows(Permission.Buy));
        GoAccount = Action(() => Open("Mi cuenta", new AccountModel(accounts, backups, dialogs, session, signOut)));
        SignOut = Action(() =>
        {
            if (!salesModel.HasItems || Dialogs.Confirm("El carrito pendiente se perderá al cerrar sesión. ¿Deseas continuar?"))
                signOut();
        });
        if (Allows(Permission.ViewDashboard))
            GoDashboard.Execute(null);
        else
            GoAccount.Execute(null);

        bool Allows(Permission permission) => session.Permissions.HasFlag(permission);
    }

    public string User { get; }
    public string Title { get => title; private set => Set(ref title, value); }
    public ViewModel Page { get => page; private set => Set(ref page, value); }
    public ICommand GoDashboard { get; }
    public ICommand GoSales { get; }
    public ICommand GoDispatch { get; }
    public ICommand GoCatalog { get; }
    public ICommand GoInventory { get; }
    public ICommand GoPurchases { get; }
    public ICommand GoAccount { get; }
    public ICommand SignOut { get; }

    private void Open(string title, ViewModel model, Action? refresh = null)
    {
        refresh?.Invoke();
        Title = title;
        Page = model;
    }
}

public sealed class LoginModel : ViewModel
{
    private readonly IAccounts accounts;
    private readonly Action<Session> signedIn;

    public LoginModel(IAccounts accounts, DesktopDialogs dialogs, Action<Session> signedIn) : base(dialogs)
    {
        this.accounts = accounts;
        this.signedIn = signedIn;
        Setup = accounts.NeedsSetup();
    }

    public bool Setup { get; }
    public string Heading => Setup ? "Crea tu cuenta" : "Inicia sesión";
    public string Introduction => Setup ? "Registra la cuenta propietaria de esta tienda." : "Ingresa para continuar con la operación de tu tienda.";
    public string Button => Setup ? "Crear cuenta" : "Ingresar";
    public string Login { get; set; } = "";
    public string Name { get; set; } = "";

    public void Submit(string password, string confirmation) => Run(() =>
    {
        if (Setup && password != confirmation)
            throw new BusinessException("Las contraseñas no coinciden.");
        signedIn(Setup ? accounts.CreateOwner(Name, Login, password) : accounts.SignIn(Login, password));
    });
}

public sealed class AccountModel : ViewModel
{
    private readonly IAccounts accounts;
    private readonly Session session;

    public AccountModel(IAccounts accounts, IBackups backups, DesktopDialogs dialogs, Session session, Action signOut) : base(dialogs)
    {
        this.accounts = accounts;
        this.session = session;
        CreateBackup = Action(() =>
        {
            var file = Dialogs.ChooseBackupDestination();
            if (file is null)
                return;
            backups.Create(session, file);
            Show("Respaldo guardado.");
        });
        RestoreBackup = Action(() =>
        {
            var file = Dialogs.ChooseBackupSource();
            if (file is null || !Dialogs.Confirm("¿Restaurar el respaldo? Se reemplazarán los datos actuales y se cerrará tu sesión. Se guardará una copia previa."))
                return;
            var recovery = backups.Restore(session, file);
            Dialogs.Inform($"Respaldo restaurado. Vuelve a ingresar con la cuenta del respaldo.\n\nCopia previa guardada en {recovery}");
            signOut();
        });
    }

    public string Name => session.Name;
    public bool CanBackUp => session.Permissions.HasFlag(Permission.ManageBackups);
    public ICommand CreateBackup { get; }
    public ICommand RestoreBackup { get; }

    public void Change(string current, string replacement, string confirmation) => Run(() =>
    {
        if (replacement != confirmation)
            throw new BusinessException("Las contraseñas nuevas no coinciden.");
        accounts.ChangePassword(session, current, replacement);
        Show("Contraseña actualizada.");
    });
}
