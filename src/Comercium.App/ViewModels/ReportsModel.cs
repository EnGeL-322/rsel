using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Comercium.Application;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public sealed record ReportChoice(ReportKind Kind, string Name);

public sealed class ReportsModel : ViewModel
{
    private readonly Session session;
    private readonly IReporting reporting;
    private ReportData? report;
    private DataView? rows;
    private DateTime? from = DateTime.Today.AddDays(1 - DateTime.Today.Day);
    private DateTime? through = DateTime.Today;
    private string answer = "";
    private string overview = "";

    public ReportsModel(Session session, DesktopDialogs dialogs, IReporting reporting, bool analysis) : base(dialogs)
    {
        this.session = session;
        this.reporting = reporting;
        Analysis = analysis;
        Choice = Choices.Single(choice => choice.Kind == (Analysis ? ReportKind.ProductosVendidos : ReportKind.Ventas));
        Apply = Action(Query);
        Daily = Action(() => Period(DateTime.Today));
        Weekly = Action(() => Period(DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7))));
        Monthly = Action(() => Period(DateTime.Today.AddDays(1 - DateTime.Today.Day)));
        Pdf = Action(() => Export(excel: false), () => report != null);
        Excel = Action(() => Export(excel: true), () => report != null);
        Ask = Action(AnswerQuestion);
        Run(() =>
        {
            Categories = reporting.Categories(session);
            Category = Categories.FirstOrDefault();
            Notify(nameof(Categories));
            Notify(nameof(Category));
            Query();
        });
    }

    public bool Analysis { get; }
    public IReadOnlyList<CategoryChoice> Categories { get; private set; } = [];
    public CategoryChoice? Category { get; set; }
    public ReportChoice[] Choices { get; } = [new(ReportKind.Ventas, "Ventas"), new(ReportKind.ProductosVendidos, "Productos vendidos"),
        new(ReportKind.Inventario, "Inventario actual"), new(ReportKind.SinMovimiento, "Sin movimientos"), new(ReportKind.SinVentas, "Sin ventas"),
        new(ReportKind.Compras, "Compras"), new(ReportKind.Despachos, "Despachos"), new(ReportKind.Finanzas, "Finanzas"),
        new(ReportKind.Rotación, "Rotación"), new(ReportKind.Reposición, "Reposición")];
    public ReportChoice? Choice { get; set; }
    public DateTime? From { get => from; set => Set(ref from, value); }
    public DateTime? Through { get => through; set => Set(ref through, value); }
    public DataView? Rows { get => rows; private set => Set(ref rows, value); }
    public string Title => report == null ? "" : $"{report.Title} · {report.Rows.Count:N0} {(report.Rows.Count == 1 ? "registro" : "registros")}";
    public string Subtitle => report?.Subtitle ?? "";
    public string Question { get; set; } = "";
    public string Answer { get => answer; private set => Set(ref answer, value); }
    public string Overview { get => overview; private set => Set(ref overview, value); }
    public ICommand Apply { get; }
    public ICommand Daily { get; }
    public ICommand Weekly { get; }
    public ICommand Monthly { get; }
    public ICommand Pdf { get; }
    public ICommand Excel { get; }
    public ICommand Ask { get; }

    private void Period(DateTime start)
    {
        From = start;
        Through = DateTime.Today;
        Query();
    }

    private void Query()
    {
        if (From is not { } start || Through is not { } end)
            throw new BusinessException("Selecciona la fecha inicial y final.");
        var first = DateOnly.FromDateTime(start);
        var last = DateOnly.FromDateTime(end);
        Load(new ReportFilter(Choice?.Kind ?? (Analysis ? ReportKind.ProductosVendidos : ReportKind.Ventas), first, last, Category?.Id));
        if (Analysis)
        {
            var stats = reporting.Compare(session, first, last);
            if (stats.Sales == 0 && stats.PreviousSales == 0)
            {
                Overview = "Todavía no hay ventas confirmadas en estos períodos para comparar.";
                return;
            }
            var change = stats.PreviousRevenue == 0 ? "variación porcentual sin base" :
                $"variación {(stats.Revenue - (decimal)stats.PreviousRevenue) / stats.PreviousRevenue:+0.0%;-0.0%;0.0%}";
            Overview = $"Toda la tienda · Ventas {Display.Money(stats.Revenue)} · Margen bruto {Display.Money(stats.Revenue - stats.Cost)}\n" +
                $"Período anterior {stats.PreviousFrom:dd/MM}–{stats.PreviousThrough:dd/MM} · {Display.Money(stats.PreviousRevenue)} · {change}";
        }
    }

    private void AnswerQuestion()
    {
        var normalized = Question.Normalize(NormalizationForm.FormD);
        var text = new string(normalized.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray())
            .ToLowerInvariant();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var month = today.AddDays(1 - today.Day);
        if (text.Contains("mas vendido") || text.Contains("repon") || text.Contains("debo comprar") || text.Contains("sin vender") || text.Contains("sin ventas"))
        {
            var replenishment = text.Contains("repon") || text.Contains("debo comprar");
            var inactive = text.Contains("sin vender") || text.Contains("sin ventas");
            var match = Regex.Match(text, @"\b([1-9][0-9]{0,2})\s*dias\b");
            var days = match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 30;
            var first = inactive ? today.AddDays(1 - days) : month;
            var kind = replenishment ? ReportKind.Reposición : inactive ? ReportKind.SinVentas : ReportKind.ProductosVendidos;
            LoadAnswer(kind, first, today);
            Answer = report!.Rows.Count == 0 ? "Todavía no hay registros que coincidan con la consulta." :
                $"{report.Rows.Count} {(report.Rows.Count == 1 ? "producto" : "productos")} · Consulta el detalle en la tabla.";
            return;
        }
        if (text.Contains("vendi") || text.Contains("ventas") || text.Contains("gane") || text.Contains("utilidad") || text.Contains("margen"))
        {
            var profit = text.Contains("gane") || text.Contains("utilidad") || text.Contains("margen");
            var first = text.Contains("hoy") ? today : month;
            var stats = reporting.Compare(session, first, today);
            LoadAnswer(profit ? ReportKind.Finanzas : ReportKind.Ventas, first, today);
            Answer = stats.Sales == 0 ? "Todavía no hay ventas confirmadas en ese período." : profit
                ? $"Margen bruto {Display.Money(stats.Revenue - stats.Cost)}. Resultado después de gastos registrados {Display.Money(stats.Revenue - stats.Cost - stats.Expenses)}."
                : $"Registraste {Display.Money(stats.Revenue)} en {stats.Sales} {(stats.Sales == 1 ? "venta confirmada" : "ventas confirmadas")}.";
            return;
        }
        Answer = "Prueba con «¿Cuánto vendí hoy?», «¿Qué productos debo comprar?» o «¿Cuánto gané este mes?».";
    }

    private void LoadAnswer(ReportKind kind, DateOnly first, DateOnly last)
    {
        Choice = Choices.Single(row => row.Kind == kind);
        Category = Categories.FirstOrDefault();
        From = first.ToDateTime(TimeOnly.MinValue);
        Through = last.ToDateTime(TimeOnly.MinValue);
        Notify(nameof(Choice));
        Notify(nameof(Category));
        Query();
    }

    private void Load(ReportFilter filter)
    {
        var result = reporting.Read(session, filter);
        var table = new DataTable();
        foreach (var column in result.Columns)
            table.Columns.Add(column);
        foreach (var row in result.Rows)
            table.Rows.Add(row.Select(ReportExporter.Cell).ToArray());
        report = result;
        Rows = table.DefaultView;
        Notify(nameof(Title));
        Notify(nameof(Subtitle));
    }

    private void Export(bool excel)
    {
        if (report == null || Dialogs.SaveDocument($"Comercium-{DateTime.Today:yyyy-MM-dd}", excel) is not { } file)
            return;
        ReportExporter.Save(report, file, excel);
        Show(excel ? "Libro Excel guardado." : "Documento PDF guardado.");
    }
}
