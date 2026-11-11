using System.Windows.Input;
using Comercium.Application;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public sealed class DispatchModel : ViewModel
{
    private readonly Session session;
    private readonly IDispatch dispatch;
    private IReadOnlyList<DispatchRow> orders = [];
    private DispatchRow? selected;
    private PickLine? selectedLine;
    private DispatchOrder? order;
    private bool history;

    public DispatchModel(Session session, DesktopDialogs dialogs, IDispatch dispatch) : base(dialogs)
    {
        this.session = session;
        this.dispatch = dispatch;
        Reload = Action(() => Refresh());
        Verify = Action(Toggle, () => SelectedLine is not null && Order?.Status == DispatchStatus.EnPreparación);
        Advance = Action(AdvanceOrder, CanAdvance);
        Print = Action(PrintList, () => Order is not null);
        Run(() => Refresh());
    }

    public bool History { get => history; set { Set(ref history, value); Run(() => Refresh()); } }
    public IReadOnlyList<DispatchRow> Orders { get => orders; private set => Set(ref orders, value); }
    public DispatchRow? Selected
    {
        get => selected;
        set
        {
            Set(ref selected, value);
            Run(() => Load(value?.Id));
        }
    }
    public PickLine? SelectedLine
    {
        get => selectedLine;
        set { Set(ref selectedLine, value); Notify(nameof(VerifyLabel)); }
    }
    public DispatchOrder? Order { get => order; private set => Set(ref order, value); }
    public string VerifyLabel => SelectedLine?.Verified == true ? "Quitar verificación" : "Verificar producto";
    public string AdvanceLabel => Order?.Status switch
    {
        DispatchStatus.Pendiente => "Iniciar preparación",
        DispatchStatus.EnPreparación => "Marcar listo",
        DispatchStatus.ListoParaRecoger => "Confirmar entrega",
        _ => "Avanzar pedido"
    };
    public string Progress => Order is null ? "Selecciona un pedido." : $"{Order.Lines.Count(line => line.Verified)} de {Order.Lines.Count} líneas verificadas";
    public string Timeline => Order is null ? "" : string.Join("  ·  ", new[]
    {
        Event("Preparación", Order.StartedUtc, Order.StartedBy), Event("Listo", Order.ReadyUtc, Order.ReadyBy),
        Event("Entrega", Order.DeliveredUtc, Order.DeliveredBy)
    }.Where(text => text != ""));

    public ICommand Reload { get; }
    public ICommand Verify { get; }
    public ICommand Advance { get; }
    public ICommand Print { get; }

    private void Refresh(long? selectId = null)
    {
        var id = selectId ?? Selected?.Id;
        Orders = dispatch.List(session, History);
        Selected = Orders.SingleOrDefault(row => row.Id == id) ?? Orders.FirstOrDefault();
    }

    private void Load(long? id)
    {
        var lineId = SelectedLine?.Id;
        Order = id.HasValue ? dispatch.Load(session, id.Value) : null;
        SelectedLine = Order?.Lines.SingleOrDefault(line => line.Id == lineId);
        Notify(nameof(Progress));
        Notify(nameof(Timeline));
        Notify(nameof(AdvanceLabel));
    }

    private bool CanAdvance() => Order?.Status == DispatchStatus.Pendiente || Order is
        { Status: DispatchStatus.EnPreparación or DispatchStatus.ListoParaRecoger } && Order.Lines.All(line => line.Verified);

    private void Toggle()
    {
        if (Order is not { } current || SelectedLine is not { } line)
            return;
        dispatch.Verify(session, current.Id, line.Id, !line.Verified);
        Refresh(current.Id);
        SelectedLine = Order?.Lines.SingleOrDefault(row => row.Id == line.Id);
        Show(line.Verified ? "Verificación retirada." : "Producto verificado.");
    }

    private void AdvanceOrder()
    {
        if (Order is not { } current)
            return;
        if (current.Status == DispatchStatus.ListoParaRecoger && !Dialogs.Confirm($"¿Registrar la entrega en tienda del pedido N.º {current.Id}?"))
            return;
        dispatch.Advance(session, current.Id, current.Status);
        Refresh(current.Id);
        Show($"Pedido N.º {current.Id} actualizado.");
    }

    private void PrintList()
    {
        if (Order is not { } current)
            return;
        Dialogs.Print($"Comercium · Lista de despacho N.º {current.Id}",
            current.Lines.Select(line => $"[    ]  {line.Quantity} × {line.Name} · {line.Code} · {(line.Location == "" ? "Sin ubicación" : line.Location)}"));
    }

    private static string Event(string label, DateTime? at, string? name) => at.HasValue ? $"{label} {Display.Local(at.Value):g} · {name}" : "";
}
