using System.Collections.ObjectModel;
using System.Windows.Input;
using Comercium.Application;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public sealed record CartItem(int ProductId, string Code, string Name, bool Wholesale, int Quantity, long Price)
{
    public string Tariff => Wholesale ? "Mayorista" : "Minorista";
    public long Subtotal => Quantity * Price;
}

public sealed class SalesModel : ViewModel
{
    private readonly Session session;
    private readonly ICatalog catalog;
    private readonly ISales sales;
    private Guid operationId = Guid.NewGuid();
    private IReadOnlyList<ProductRow> products = [];
    private IReadOnlyList<SaleRow> history = [];
    private ProductRow? selectedProduct;
    private CartItem? selectedItem;
    private SaleRow? selectedSale;
    private string quantity = "1";
    private string cartQuantity = "1";
    private string discount = "0";
    private string received = "";
    private string voidReason = "";
    private PaymentMethod method;

    public SalesModel(Session session, DesktopDialogs dialogs, ICatalog catalog, ISales sales) : base(dialogs)
    {
        this.session = session;
        this.catalog = catalog;
        this.sales = sales;
        Search = Action(SearchProducts);
        Add = Action(AddLine, () => SelectedProduct is not null);
        Remove = Action(RemoveLine, () => SelectedItem is not null);
        UpdateQuantity = Action(UpdateLine, () => SelectedItem is not null);
        Confirm = Action(ConfirmSale, () => HasItems);
        ExactCash = Action(() => Received = Display.Input(Total), () => HasItems && IsCash);
        PrintReceipt = Action(Print, () => SelectedSale is not null);
        VoidSale = Action(Void, () => SelectedSale is { Voided: false } && session.Permissions.HasFlag(Permission.VoidSale));
    }

    public string Term { get; set; } = "";
    public bool Wholesale { get; set; }
    public bool CanDiscount => session.Permissions.HasFlag(Permission.ApplyDiscount);
    public PaymentMethod[] Methods { get; } = Enum.GetValues<PaymentMethod>();
    public ObservableCollection<CartItem> Cart { get; } = [];
    public bool HasItems => Cart.Count != 0;
    public IReadOnlyList<ProductRow> Products { get => products; private set => Set(ref products, value); }
    public IReadOnlyList<SaleRow> History { get => history; private set => Set(ref history, value); }
    public ProductRow? SelectedProduct { get => selectedProduct; set => Set(ref selectedProduct, value); }
    public SaleRow? SelectedSale { get => selectedSale; set => Set(ref selectedSale, value); }
    public CartItem? SelectedItem
    {
        get => selectedItem;
        set
        {
            Set(ref selectedItem, value);
            if (value is not null)
                CartQuantity = value.Quantity.ToString();
        }
    }
    public string Quantity { get => quantity; set => Set(ref quantity, value); }
    public string CartQuantity { get => cartQuantity; set => Set(ref cartQuantity, value); }
    public string VoidReason { get => voidReason; set => Set(ref voidReason, value); }
    public string Discount { get => discount; set { Set(ref discount, value); NotifyTotals(); } }
    public string Received { get => received; set { Set(ref received, value); Notify(nameof(ChangeText)); } }
    public PaymentMethod Method { get => method; set { Set(ref method, value); Notify(nameof(IsCash)); Notify(nameof(ChangeText)); } }
    public bool IsCash => Method == PaymentMethod.Efectivo;
    public long Subtotal => Cart.Sum(item => item.Subtotal);
    public long Total => Math.Max(0, Subtotal - PreviewMoney(Discount));
    public string SubtotalText => Display.Money(Subtotal);
    public string TotalText => Display.Money(Total);
    public string ChangeText => Display.Money(IsCash ? Math.Max(0, PreviewMoney(Received) - Total) : 0);

    public ICommand Search { get; }
    public ICommand Add { get; }
    public ICommand Remove { get; }
    public ICommand UpdateQuantity { get; }
    public ICommand Confirm { get; }
    public ICommand ExactCash { get; }
    public ICommand PrintReceipt { get; }
    public ICommand VoidSale { get; }

    public void Refresh() => Run(() => { SearchProducts(); RefreshHistory(); });

    private void SearchProducts() => Products = catalog.Search(session, Term, activeOnly: true);

    private void RefreshHistory(long? selectId = null)
    {
        var id = selectId ?? SelectedSale?.Id;
        History = sales.Recent(session);
        SelectedSale = History.SingleOrDefault(row => row.Id == id);
    }

    private void AddLine()
    {
        if (SelectedProduct is not { } product)
            return;
        var amount = Display.ParseCount(Quantity, "cantidad");
        var existing = Cart.SingleOrDefault(row => row.ProductId == product.Id && row.Wholesale == Wholesale);
        ValidateQuantity(product, amount, excluded: null);
        var item = new CartItem(product.Id, product.Code, product.Name, Wholesale,
            amount + (existing?.Quantity ?? 0), Wholesale ? product.Wholesale : product.Retail);
        if (existing is null)
            Cart.Add(item);
        else
            Cart[Cart.IndexOf(existing)] = item;
        SelectedItem = item;
        Quantity = "1";
        NotifyTotals();
    }

    private void UpdateLine()
    {
        if (SelectedItem is not { } item)
            return;
        var product = catalog.Search(session, item.Code, activeOnly: true).SingleOrDefault(row => row.Id == item.ProductId)
            ?? throw new BusinessException("El producto dejó de estar disponible. Retíralo del carrito.");
        var amount = Display.ParseCount(CartQuantity, "cantidad");
        ValidateQuantity(product, amount, item);
        var replacement = item with { Quantity = amount, Price = item.Wholesale ? product.Wholesale : product.Retail };
        Cart[Cart.IndexOf(item)] = replacement;
        SelectedItem = replacement;
        NotifyTotals();
    }

    private void ValidateQuantity(ProductRow product, int amount, CartItem? excluded)
    {
        var inCart = Cart.Where(item => item.ProductId == product.Id && item != excluded).Sum(item => (long)item.Quantity);
        if (amount == 0 || inCart + amount > product.Stock || inCart + amount > 1_000_000)
            throw new BusinessException($"Cantidad inválida. Existencias de {product.Name}: {product.Stock}.");
    }

    private void RemoveLine()
    {
        if (SelectedItem is { } item)
            Cart.Remove(item);
        NotifyTotals();
    }

    private void ConfirmSale()
    {
        var amount = Display.ParseMoney(Discount, "descuento");
        var cash = IsCash ? Display.ParseMoney(Received, "efectivo recibido") : 0;
        var id = sales.Confirm(session, new Checkout(operationId, Cart.Select(item =>
            new CartLine(item.ProductId, item.Quantity, item.Wholesale, item.Price)).ToList(), amount, Method, cash));
        var change = IsCash ? cash - Total : 0;
        operationId = Guid.NewGuid();
        Cart.Clear();
        Discount = "0";
        Received = "";
        NotifyTotals();
        SearchProducts();
        RefreshHistory(id);
        Show($"Venta y pedido N.º {id} registrados. Cambio: {Display.Money(change)}.");
    }

    private void Print()
    {
        if (SelectedSale is not { } row)
            return;
        var receipt = sales.Receipt(session, row.Id);
        var lines = new List<string> { $"{Display.Local(receipt.AtUtc):g} · {receipt.Cashier}", receipt.Voided ? "ANULADO" : "Venta en tienda" };
        lines.AddRange(receipt.Lines.Select(line => $"{line.Quantity} × {line.Name} ({line.Code}) · {Display.Money(line.UnitPrice * line.Quantity)}"));
        lines.AddRange([$"Subtotal {Display.Money(receipt.Subtotal)}", $"Descuento {Display.Money(receipt.Discount)}",
            $"Total {Display.Money(receipt.Total)} · {receipt.Method}"]);
        if (receipt.Method == PaymentMethod.Efectivo)
            lines.Add($"Recibido {Display.Money(receipt.Received)} · Cambio {Display.Money(receipt.Received - receipt.Total)}");
        Dialogs.Print($"Comercium · Comprobante interno N.º {row.Id}", lines);
    }

    private void Void()
    {
        if (SelectedSale is not { } row)
            return;
        if (string.IsNullOrWhiteSpace(VoidReason))
            throw new BusinessException("Escribe el motivo de anulación.");
        if (!Dialogs.Confirm($"¿Anular la venta N.º {row.Id} y devolver sus unidades al inventario?"))
            return;
        sales.Void(session, row.Id, VoidReason);
        VoidReason = "";
        SearchProducts();
        RefreshHistory(row.Id);
        Show("Venta anulada. El historial y la reversión quedaron registrados.");
    }

    private void NotifyTotals()
    {
        Notify(nameof(HasItems));
        Notify(nameof(SubtotalText));
        Notify(nameof(TotalText));
        Notify(nameof(ChangeText));
    }

    private static long PreviewMoney(string value)
    {
        try { return Display.ParseMoney(value, "importe"); }
        catch (BusinessException) { return 0; }
    }
}
