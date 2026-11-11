using System.Collections.ObjectModel;
using System.Windows.Input;
using Comercium.Application;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public sealed record PurchaseCartLine(ProductRow Product, int Quantity, long UnitCost)
{
    public long Total => Quantity * UnitCost;
}

public sealed class PurchasesModel : ViewModel
{
    private readonly IPurchases service;
    private readonly Session session;
    private Guid receivingId = Guid.NewGuid();
    private Guid paymentId = Guid.NewGuid();
    private IReadOnlyList<SupplierRow> suppliers = [];
    private IReadOnlyList<PurchaseRow> history = [];
    private IReadOnlyList<SupplierCost> costs = [];
    private PurchaseReceipt? detail;
    private SupplierRow? supplier;
    private ProductRow? product;
    private PurchaseRow? selectedPurchase;
    private PurchaseCartLine? selectedLine;
    private string supplierName = "";
    private string supplierPhone = "";
    private string reference = "";
    private string amount = "";
    private bool outstandingOnly;

    public PurchasesModel(Session session, DesktopDialogs dialogs, IPurchases service, ICatalog catalog) : base(dialogs)
    {
        this.session = session;
        this.service = service;
        SaveSupplier = Action(Save);
        NewSupplier = Action(() => Supplier = null);
        Add = Action(AddProduct, () => Product is not null);
        Remove = Action(() =>
        {
            if (SelectedLine is { } line)
                Cart.Remove(line);
            Notify(nameof(Total));
        }, () => SelectedLine is not null);
        Receive = Action(ReceiveStock, () => Cart.Count > 0 && Supplier is not null);
        Pay = Action(RegisterPayment, () => SelectedPurchase is { Outstanding: > 0 });
        Run(() =>
        {
            Suppliers = service.Suppliers(session);
            Products = catalog.Search(session, "", activeOnly: true);
            Notify(nameof(Products));
            RefreshHistory();
        });
    }

    public ObservableCollection<PurchaseCartLine> Cart { get; } = [];
    public IReadOnlyList<SupplierRow> Suppliers { get => suppliers; private set => Set(ref suppliers, value); }
    public IReadOnlyList<ProductRow> Products { get; private set; } = [];
    public IReadOnlyList<PurchaseRow> History { get => history; private set => Set(ref history, value); }
    public IReadOnlyList<SupplierCost> Costs { get => costs; private set => Set(ref costs, value); }
    public PurchaseReceipt? Detail { get => detail; private set => Set(ref detail, value); }
    public PaymentMethod[] Methods { get; } = Enum.GetValues<PaymentMethod>();
    public PaymentMethod Method { get; set; }
    public string SupplierName { get => supplierName; set => Set(ref supplierName, value); }
    public string SupplierPhone { get => supplierPhone; set => Set(ref supplierPhone, value); }
    public string Reference { get => reference; set => Set(ref reference, value); }
    public string Amount { get => amount; set => Set(ref amount, value); }
    public string Quantity { get; set; } = "1";
    public string UnitCost { get; set; } = "";
    public string Total => Display.Money(Cart.Sum(line => line.Total));
    public PurchaseCartLine? SelectedLine { get => selectedLine; set => Set(ref selectedLine, value); }
    public bool OutstandingOnly { get => outstandingOnly; set { Set(ref outstandingOnly, value); Run(() => RefreshHistory()); } }

    public SupplierRow? Supplier
    {
        get => supplier;
        set { Set(ref supplier, value); SupplierName = value?.Name ?? ""; SupplierPhone = value?.Phone ?? ""; }
    }
    public ProductRow? Product
    {
        get => product;
        set { Set(ref product, value); Run(() => Costs = value is null ? [] : service.Compare(session, value.Id)); }
    }
    public PurchaseRow? SelectedPurchase
    {
        get => selectedPurchase;
        set { Set(ref selectedPurchase, value); Run(() => Detail = value is null ? null : service.Detail(session, value.Id)); }
    }

    public ICommand SaveSupplier { get; }
    public ICommand NewSupplier { get; }
    public ICommand Add { get; }
    public ICommand Remove { get; }
    public ICommand Receive { get; }
    public ICommand Pay { get; }

    private void Save()
    {
        var id = service.SaveSupplier(session, new SupplierRow(Supplier?.Id ?? 0, SupplierName, SupplierPhone));
        Suppliers = service.Suppliers(session);
        Supplier = Suppliers.Single(row => row.Id == id);
        Show("Proveedor guardado.");
    }

    private void AddProduct()
    {
        if (Product is not { } row)
            return;
        var quantity = Display.ParseCount(Quantity, "cantidad");
        var cost = Display.ParseMoney(UnitCost, "costo unitario");
        if (quantity is < 1 or > 1_000_000 || cost is < 1 or > 100_000_000)
            throw new BusinessException("Ingresa una cantidad y un costo unitario válidos.");
        var previous = Cart.SingleOrDefault(line => line.Product.Id == row.Id);
        var replacement = new PurchaseCartLine(row, quantity, cost);
        if (previous is null)
            Cart.Add(replacement);
        else
            Cart[Cart.IndexOf(previous)] = replacement;
        Notify(nameof(Total));
    }

    private void ReceiveStock()
    {
        if (Supplier is not { } vendor || !Dialogs.Confirm($"¿Registrar la recepción de {Cart.Count} líneas de {vendor.Name} por {Total}?"))
            return;
        var id = service.Receive(session, new ReceivePurchase(receivingId, vendor.Id, Reference,
            Cart.Select(line => new PurchaseItem(line.Product.Id, line.Quantity, line.UnitCost)).ToList()));
        receivingId = Guid.NewGuid();
        Cart.Clear();
        Reference = "";
        Notify(nameof(Total));
        RefreshHistory(id);
        Costs = Product is { } row ? service.Compare(session, row.Id) : [];
        Show($"Compra N.º {id} registrada. Las existencias se actualizaron.");
    }

    private void RegisterPayment()
    {
        if (SelectedPurchase is not { } row)
            return;
        var paid = Display.ParseMoney(Amount, "pago");
        if (!Dialogs.Confirm($"¿Registrar un pago de {Display.Money(paid)} para la compra N.º {row.Id}?"))
            return;
        service.Pay(session, row.Id, paymentId, paid, Method);
        paymentId = Guid.NewGuid();
        Amount = "";
        RefreshHistory(row.Id);
        Show("Pago registrado.");
    }

    private void RefreshHistory(long? id = null)
    {
        id ??= SelectedPurchase?.Id;
        History = service.History(session, OutstandingOnly);
        SelectedPurchase = History.SingleOrDefault(row => row.Id == id);
    }
}
