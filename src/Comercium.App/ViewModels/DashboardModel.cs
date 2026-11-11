using System.Windows.Input;
using Comercium.Application;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Comercium.App.ViewModels;

public sealed record Metric(string Name, string Value, string Caption);

public sealed class DashboardModel : ViewModel
{
    public DashboardModel(Session session, DesktopDialogs dialogs, IDashboard dashboard, ICommand newSale) : base(dialogs)
    {
        NewSale = newSale;
        var data = dashboard.Read(session);
        Metrics = [new("Ventas de hoy", Display.Money(data.Today), "Cobros confirmados"),
            new("Ventas del mes", Display.Money(data.Month), "Acumulado del mes"),
            new("Margen bruto del mes", Display.Money(data.Margin), "Ventas menos costo de productos"),
            new("Productos activos", data.Products.ToString(), "Catálogo disponible"),
            new("Stock bajo", data.LowStock.ToString(), "Con existencias en el mínimo o menos"),
            new("Agotados", data.OutOfStock.ToString(), "Productos activos sin existencias")];
        BestSellers = data.BestSellers;
        RecentPurchases = data.RecentPurchases;
        Pending = data.PendingDispatches;
        HasWeeklySales = data.Week.Any(day => day.Amount != 0);
        Series = [new ColumnSeries<double>
        {
            Name = "Ventas (S/)", Values = data.Week.Select(item => item.Amount / 100d).ToArray(),
            Fill = new SolidColorPaint(SKColor.Parse("#2784E6")), Stroke = null, MaxBarWidth = 38
        }];
        var labels = new SolidColorPaint(SKColor.Parse("#536981"));
        XAxes = [new Axis { Labels = data.Week.Select(item => item.Day.ToString("ddd dd")).ToArray(), MinStep = 1, LabelsPaint = labels }];
        YAxes = [new Axis { MinLimit = 0, MinStep = 0.01, LabelsPaint = labels,
            SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#DCE5EF")), Labeler = value => $"S/ {value:0.##}" }];
    }

    public IReadOnlyList<Metric> Metrics { get; }
    public IReadOnlyList<BestSeller> BestSellers { get; }
    public IReadOnlyList<PurchaseRow> RecentPurchases { get; }
    public bool EmptyPurchases => RecentPurchases.Count == 0;
    public bool EmptyBestSellers => BestSellers.Count == 0;
    public bool HasWeeklySales { get; }
    public bool EmptyWeek => !HasWeeklySales;
    public int Pending { get; }
    public ISeries[] Series { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }
    public ICommand NewSale { get; }
}
