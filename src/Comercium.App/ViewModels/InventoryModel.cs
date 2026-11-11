using System.Windows.Input;
using Comercium.Application;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public sealed class InventoryModel : ViewModel
{
    private readonly Session session;
    private readonly ICatalog catalog;
    private IReadOnlyList<ProductRow> products = [];
    private IReadOnlyList<MovementRow> movements = [];
    private ProductRow? selected;
    private string quantity = "";
    private string reason = "";
    private Guid operationId = Guid.NewGuid();

    public InventoryModel(Session session, DesktopDialogs dialogs, ICatalog catalog) : base(dialogs)
    {
        this.session = session;
        this.catalog = catalog;
        Search = Action(Refresh);
        Save = Action(SaveMovement, () => Selected is not null);
        Run(Refresh);
    }

    public string Term { get; set; } = "";
    public string Filter { get; set; } = "Todos";
    public string[] Filters { get; } = ["Todos", "Stock bajo", "Agotados"];
    public MovementKind[] Kinds { get; } = [MovementKind.Entrada, MovementKind.Salida, MovementKind.Ajuste];
    public MovementKind Kind { get; set; }
    public string Quantity { get => quantity; set => Set(ref quantity, value); }
    public string Reason { get => reason; set => Set(ref reason, value); }
    public IReadOnlyList<ProductRow> Products { get => products; private set => Set(ref products, value); }
    public IReadOnlyList<MovementRow> Movements { get => movements; private set => Set(ref movements, value); }
    public ProductRow? Selected
    {
        get => selected;
        set
        {
            Set(ref selected, value);
            Run(() => Movements = value is null ? [] : catalog.Movements(session, value.Id));
        }
    }
    public ICommand Search { get; }
    public ICommand Save { get; }

    private void Refresh()
    {
        var id = Selected?.Id;
        Products = catalog.Search(session, Term).Where(row => Filter switch
        {
            "Stock bajo" => row.Active && row.Stock > 0 && row.Stock <= row.Minimum,
            "Agotados" => row.Active && row.Stock == 0,
            _ => true
        }).ToList();
        Selected = Products.SingleOrDefault(row => row.Id == id);
    }

    private void SaveMovement()
    {
        if (Selected is null)
            return;
        var amount = Display.ParseCount(Quantity, "cantidad");
        if (Kind != MovementKind.Entrada && !Dialogs.Confirm($"¿Registrar {Kind.ToString().ToLowerInvariant()} para {Selected.Name}?"))
            return;
        catalog.Move(session, Selected.Id, operationId, Kind, amount, Reason);
        operationId = Guid.NewGuid();
        Quantity = "";
        Reason = "";
        Refresh();
        Show("Movimiento registrado.");
    }
}
