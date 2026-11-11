namespace Comercium.Application;

public enum ReportKind { Ventas, ProductosVendidos, Inventario, SinMovimiento, SinVentas, Compras, Despachos, Finanzas, Rotación, Reposición }
public sealed record CategoryChoice(int? Id, string Name);
public sealed record ReportFilter(ReportKind Kind, DateOnly From, DateOnly Through, int? CategoryId = null);
public sealed record ReportData(string Title, string Subtitle, IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows);
public sealed record PeriodStats(int Sales, long Revenue, long Cost, long Expenses, long PreviousRevenue, int PreviousSales,
    DateOnly PreviousFrom, DateOnly PreviousThrough);

public interface IReporting
{
    IReadOnlyList<CategoryChoice> Categories(Session session);
    ReportData Read(Session session, ReportFilter filter);
    PeriodStats Compare(Session session, DateOnly from, DateOnly through);
}
