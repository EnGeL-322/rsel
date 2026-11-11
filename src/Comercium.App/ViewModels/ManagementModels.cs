using System.Windows.Input;
using Comercium.Application;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public sealed class CustomersModel : ViewModel
{
    private readonly ICustomers customers;
    private readonly Session session;
    private IReadOnlyList<CustomerSummary> rows = [];
    private IReadOnlyList<SaleRow> history = [];
    private CustomerSummary? selected;
    private SaleRow? sale;
    private CustomerForm form = new();

    public CustomersModel(Session session, DesktopDialogs dialogs, ICustomers customers, ISales sales) : base(dialogs)
    {
        this.customers = customers;
        this.session = session;
        Find = Action(() => Refresh());
        New = Action(() => Selected = null);
        Save = Action(() =>
        {
            var id = customers.Save(session, new CustomerCard(Form.Id, Form.Name, Form.Document, Form.Phone, Form.Wholesale, Form.Active));
            Refresh(id);
            Show("Cliente guardado.");
        });
        SaveReceipt = Action(() =>
        {
            if (Sale is { } row && Dialogs.SaveDocument($"Comercium-venta-{row.Id}", excel: false) is { } file)
                ReportExporter.Save(ReportExporter.Receipt(sales.Receipt(session, row.Id)), file, excel: false);
        }, () => Sale != null);
        Run(() => Refresh());
    }

    public string Term { get; set; } = "";
    public IReadOnlyList<CustomerSummary> Rows { get => rows; private set => Set(ref rows, value); }
    public IReadOnlyList<SaleRow> History { get => history; private set => Set(ref history, value); }
    public SaleRow? Sale { get => sale; set => Set(ref sale, value); }
    public CustomerForm Form { get => form; private set => Set(ref form, value); }
    public CustomerSummary? Selected
    {
        get => selected;
        set
        {
            Set(ref selected, value);
            var card = value?.Customer;
            Form = card == null ? new CustomerForm() : new CustomerForm { Id = card.Id, Name = card.Name, Document = card.Document ?? "",
                Phone = card.Phone, Active = card.Active, Wholesale = card.Wholesale };
            Run(() => History = card == null ? [] : customers.History(session, card.Id));
        }
    }
    public ICommand Find { get; }
    public ICommand New { get; }
    public ICommand Save { get; }
    public ICommand SaveReceipt { get; }

    private void Refresh(int? id = null)
    {
        id ??= Selected?.Customer.Id;
        Rows = customers.Search(session, Term);
        Selected = Rows.FirstOrDefault(row => row.Customer.Id == id);
    }
}

public sealed class CustomerForm
{
    public int Id { get; init; }
    public string Name { get; set; } = "";
    public string Document { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool Active { get; set; } = true;
    public bool Wholesale { get; set; }
}

public sealed class CashModel : ViewModel
{
    private readonly ICash cash;
    private readonly Session session;
    private CashView data = new(null, []);
    private CashShift? selected;
    private IReadOnlyList<CashMovement> entries = [];
    private string opening = "0";
    private string amount = "";
    private string note = "";
    private string counted = "";
    private Guid operation = Guid.NewGuid();

    public CashModel(Session session, DesktopDialogs dialogs, ICash cash) : base(dialogs)
    {
        this.session = session;
        this.cash = cash;
        Open = Action(() =>
        {
            cash.Open(session, Display.ParseMoney(Opening, "saldo inicial"));
            Refresh();
            Show("Caja abierta.");
        }, () => Current == null);
        Move = Action(() =>
        {
            if (Current is not { } shift) return;
            cash.Move(session, shift.Id, operation, Kind, Display.ParseMoney(Amount, "importe"), Note);
            operation = Guid.NewGuid();
            Amount = "";
            Note = "";
            Refresh(shift.Id);
            Show("Movimiento registrado.");
        }, () => Current != null);
        Close = Action(() =>
        {
            if (Current is not { } shift) return;
            var cashCount = Display.ParseMoney(Counted, "efectivo contado");
            if (!Dialogs.Confirm($"¿Cerrar la caja con {Display.Money(cashCount)} en efectivo contado?")) return;
            cash.Close(session, shift.Id, cashCount);
            Counted = "";
            Refresh(shift.Id);
            Show($"Caja cerrada. Diferencia {Display.Money(Selected?.Difference ?? 0)}.");
        }, () => Current != null);
        Run(() => Refresh());
    }

    public CashShift? Current => data.Open;
    public string State => Current is { } shift ? $"Caja {shift.Id} abierta · Saldo esperado {Display.Money(shift.Expected)}" : "Caja cerrada";
    public IReadOnlyList<CashShift> Shifts => data.Shifts;
    public IReadOnlyList<CashMovement> Entries { get => entries; private set => Set(ref entries, value); }
    public CashShift? Selected
    {
        get => selected;
        set { Set(ref selected, value); Run(() => Entries = value == null ? [] : cash.Entries(session, value.Id)); }
    }
    public string Opening { get => opening; set => Set(ref opening, value); }
    public string Amount { get => amount; set => Set(ref amount, value); }
    public string Note { get => note; set => Set(ref note, value); }
    public string Counted { get => counted; set => Set(ref counted, value); }
    public CashKind[] Kinds { get; } = [CashKind.Ingreso, CashKind.Gasto, CashKind.Retiro];
    public CashKind Kind { get; set; }
    public ICommand Open { get; }
    public ICommand Move { get; }
    public ICommand Close { get; }

    private void Refresh(long? id = null)
    {
        data = cash.Read(session);
        Notify(nameof(Current));
        Notify(nameof(State));
        Notify(nameof(Shifts));
        Selected = Shifts.FirstOrDefault(row => row.Id == id) ?? Current ?? Shifts.FirstOrDefault();
    }
}
