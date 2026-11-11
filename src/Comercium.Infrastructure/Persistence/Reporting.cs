using Comercium.Application;
using Comercium.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Persistence;

public sealed class Reporting(StoreDatabase database) : IReporting
{
    public IReadOnlyList<CategoryChoice> Categories(Session session)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Reports);
        var rows = db.Categories.OrderBy(row => row.Name).Select(row => new CategoryChoice(row.Id, row.Name)).ToList();
        rows.Insert(0, new CategoryChoice(null, "Todas las categorías"));
        return rows;
    }

    public ReportData Read(Session session, ReportFilter filter)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Reports);
        var (start, end) = StoreDates.Range(filter.From, filter.Through);
        var category = filter.CategoryId;
        var categoryName = category.HasValue ? db.Categories.Find(category)?.Name ?? "Categoría seleccionada" : "Todas las categorías";
        var range = $"{filter.From:dd/MM/yyyy} al {filter.Through:dd/MM/yyyy} · {categoryName}";
        var sales = db.Sales.AsNoTracking().Where(s => s.OccurredUtc >= start && s.OccurredUtc < end);
        var products = db.Products.AsNoTracking().Where(p => !category.HasValue || p.CategoryId == category);

        return filter.Kind switch
        {
            ReportKind.Ventas => Sales(db, sales, category, range),
            ReportKind.ProductosVendidos => ProductsSold(db, sales, category, range),
            ReportKind.Inventario => Inventory(products, categoryName),
            ReportKind.SinMovimiento => Inactive(products.Where(p => !db.Movements.Any(m => m.ProductId == p.Id && m.OccurredUtc >= start && m.OccurredUtc < end)),
                "Productos sin movimientos de inventario", range),
            ReportKind.SinVentas => Inactive(products.Where(p => !sales.Any(s => s.VoidedUtc == null && s.Lines.Any(line => line.ProductId == p.Id))),
                "Productos sin ventas", range),
            ReportKind.Compras => Purchases(db, start, end, category, range),
            ReportKind.Despachos => Dispatches(db, sales, category, range),
            ReportKind.Finanzas => Financial(db, sales, start, end, category, range),
            ReportKind.Rotación => Turnover(db, sales, products, start, end, category, range),
            ReportKind.Reposición => Reorder(products, categoryName),
            _ => throw new BusinessException("Selecciona un reporte válido.")
        };
    }

    public PeriodStats Compare(Session session, DateOnly from, DateOnly through)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Reports);
        var (start, end) = StoreDates.Range(from, through);
        var days = through.DayNumber - from.DayNumber + 1;
        if (from.DayNumber < days)
            throw new BusinessException("El rango no permite calcular el período anterior.");
        var previous = from.AddDays(-days);
        var previousStart = StoreDates.UtcStart(previous);
        var sales = db.Sales.Where(s => s.VoidedUtc == null);
        var current = sales.Where(s => s.OccurredUtc >= start && s.OccurredUtc < end);
        var prior = sales.Where(s => s.OccurredUtc >= previousStart && s.OccurredUtc < start);
        var expenses = -db.CashEntries.Where(e => e.Kind == CashKind.Gasto && e.AtUtc >= start && e.AtUtc < end).Sum(e => e.Amount);
        return new PeriodStats(current.Count(), current.Sum(s => s.Total), current.Sum(s => s.Cost), expenses, prior.Sum(s => s.Total),
            prior.Count(), previous, from.AddDays(-1));
    }

    private static ReportData Sales(StoreContext db, IQueryable<Sale> query, int? category, string range)
    {
        var sales = query.Include(s => s.Payment).OrderByDescending(s => s.Id).ToList();
        var amounts = category.HasValue ? Amounts(db, query, category).ToLookup(line => line.SaleId) : null;
        return new ReportData("Ventas", range, ["Venta", "Fecha", "Cliente", "Pago", "Estado", "Total S/", "Costo S/"],
            sales.Where(s => amounts == null || amounts.Contains(s.Id)).Select(s => new object?[] { s.Id, Local(s.OccurredUtc),
                s.CustomerName, s.Payment.Method.ToString(), s.VoidedUtc.HasValue ? "Anulada" : "Confirmada",
                Money(amounts == null ? s.Total : amounts[s.Id].Sum(row => row.Net)),
                Money(amounts == null ? s.Cost : amounts[s.Id].Sum(row => row.Cost)) }).ToList());
    }

    private static ReportData ProductsSold(StoreContext db, IQueryable<Sale> query, int? category, string range) =>
        new("Productos vendidos", range, ["Código", "Producto", "Unidades", "Venta neta S/", "Costo S/", "Margen S/"],
            Amounts(db, query.Where(s => s.VoidedUtc == null), category).GroupBy(line => line.ProductId)
                .Select(group => new { Line = group.Last(), Units = group.Sum(row => (long)row.Quantity), Net = group.Sum(row => row.Net), Cost = group.Sum(row => row.Cost) })
                .OrderByDescending(row => row.Units).ThenBy(row => row.Line.Name).Select(row => new object?[] { row.Line.Code, row.Line.Name,
                    row.Units, Money(row.Net), Money(row.Cost), Money(row.Net - row.Cost) }).ToList());

    private static ReportData Inventory(IQueryable<Product> products, string category) => new("Inventario actual", category,
        ["Código", "Producto", "Categoría", "Ubicación", "Stock", "Mínimo", "Costo total S/"],
        products.Where(p => p.Active).OrderBy(p => p.Name).Select(p => new { p.Code, p.Name, Category = p.Category.Name, p.Location,
            p.Inventory.OnHand, p.Inventory.Minimum, p.Cost }).ToList().Select(p => new object?[] { p.Code, p.Name, p.Category, p.Location,
                p.OnHand, p.Minimum, Money(p.Cost * p.OnHand) }).ToList());

    private static ReportData Inactive(IQueryable<Product> matches, string title, string range) =>
        new(title, range, ["Código", "Producto", "Categoría", "Stock"], matches.Where(p => p.Active).OrderBy(p => p.Name)
            .Select(p => new { p.Code, p.Name, Category = p.Category.Name, p.Inventory.OnHand }).ToList()
            .Select(p => new object?[] { p.Code, p.Name, p.Category, p.OnHand }).ToList());

    private static ReportData Purchases(StoreContext db, DateTime start, DateTime end, int? category, string range)
    {
        var query = db.Purchases.Where(p => p.AtUtc >= start && p.AtUtc < end);
        if (category.HasValue)
        {
            var items = query.SelectMany(p => p.Lines.Where(line => line.Product.CategoryId == category),
                (p, line) => new { p.Id, p.AtUtc, Supplier = p.Supplier.Name, Product = line.Product.Name, line.Quantity, line.UnitCost });
            return new ReportData("Compras por categoría", range, ["Compra", "Fecha", "Proveedor", "Producto", "Unidades", "Costo S/"],
                items.OrderByDescending(row => row.Id).ToList().Select(row => new object?[] { row.Id, Local(row.AtUtc), row.Supplier, row.Product,
                    row.Quantity, Money(row.Quantity * row.UnitCost) }).ToList());
        }
        return new ReportData("Compras", range, ["Compra", "Fecha", "Proveedor", "Referencia", "Total S/", "Pagado S/", "Pendiente S/"],
            query.OrderByDescending(row => row.Id).Select(PurchaseRegister.Summary).ToList().Select(row => new object?[] { row.Id,
                Local(row.AtUtc), row.Supplier, row.Reference, Money(row.Total), Money(row.Paid), Money(row.Outstanding) }).ToList());
    }

    private static ReportData Dispatches(StoreContext db, IQueryable<Sale> query, int? category, string range)
    {
        if (category.HasValue)
            query = query.Where(s => s.Lines.Any(line => (line.CategoryId ?? db.Products.Where(p => p.Id == line.ProductId)
                .Select(p => p.CategoryId).First()) == category));
        var rows = query.OrderByDescending(s => s.Id).Select(s => new { s.Id, s.OccurredUtc, s.Dispatch.Status,
            s.Dispatch.StartedUtc, s.Dispatch.ReadyUtc, s.Dispatch.DeliveredUtc, s.Dispatch.DeliveredById }).ToList();
        var users = db.Users.ToDictionary(user => user.Id, user => user.Name);
        return new ReportData("Despachos por fecha de venta", range, ["Pedido", "Venta", "Estado", "Inicio", "Listo", "Entrega", "Entregado por"],
            rows.Select(row => new object?[] { row.Id, Local(row.OccurredUtc), Status(row.Status), Local(row.StartedUtc), Local(row.ReadyUtc),
                Local(row.DeliveredUtc), row.DeliveredById is { } id ? users.GetValueOrDefault(id) : null }).ToList());
    }

    private static ReportData Financial(StoreContext db, IQueryable<Sale> query, DateTime start, DateTime end, int? category, string range)
    {
        var sales = query.Where(s => s.VoidedUtc == null);
        var amounts = category.HasValue ? Amounts(db, sales, category) : null;
        var revenue = amounts == null ? sales.Sum(s => s.Total) : amounts.Sum(row => row.Net);
        var cost = amounts == null ? sales.Sum(s => s.Cost) : amounts.Sum(row => row.Cost);
        var rows = new List<object?[]> { new object?[] { "Ventas netas", Money(revenue) }, new object?[] { "Costo de lo vendido", Money(cost) },
            new object?[] { "Margen bruto", Money(revenue - cost) } };
        if (!category.HasValue)
        {
            var cash = db.CashEntries.Where(row => row.AtUtc >= start && row.AtUtc < end).GroupBy(row => row.Kind)
                .Select(group => new { Kind = group.Key, Total = group.Sum(row => row.Amount) }).ToDictionary(row => row.Kind, row => row.Total);
            var expenses = -cash.GetValueOrDefault(CashKind.Gasto);
            rows.Add(["Gastos registrados en caja", Money(expenses)]);
            rows.Add(["Resultado después de gastos registrados", Money(revenue - cost - expenses)]);
            foreach (var kind in Enum.GetValues<CashKind>())
                rows.Add([$"Caja · {kind}", Money(cash.GetValueOrDefault(kind))]);
            rows.Add(["Variación neta de efectivo registrado", Money(cash.Values.Sum())]);
        }
        return new ReportData("Finanzas", range, ["Concepto", "Importe S/"], rows);
    }

    private static ReportData Turnover(StoreContext db, IQueryable<Sale> sales, IQueryable<Product> products, DateTime start, DateTime end,
        int? category, string range)
    {
        var sold = Amounts(db, sales.Where(s => s.VoidedUtc == null), category).GroupBy(row => row.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(row => (long)row.Quantity));
        var balances = products.OrderBy(p => p.Name).Select(p => new { p.Id, p.Code, p.Name,
            Opening = db.Movements.Where(m => m.ProductId == p.Id && m.OccurredUtc < start).OrderByDescending(m => m.Id).Select(m => m.Balance).FirstOrDefault(),
            Closing = db.Movements.Where(m => m.ProductId == p.Id && m.OccurredUtc < end).OrderByDescending(m => m.Id).Select(m => m.Balance).FirstOrDefault() }).ToList();
        return new ReportData("Rotación de productos", $"{range} · Unidades vendidas / promedio de stock inicial y final",
            ["Código", "Producto", "Vendidas", "Stock inicial", "Stock final", "Rotación"], balances.Select(row =>
            {
                var units = sold.GetValueOrDefault(row.Id);
                var average = (row.Opening + (decimal)row.Closing) / 2;
                return new object?[] { row.Code, row.Name, units, row.Opening, row.Closing, average == 0 ? null : decimal.Round(units / average, 2) };
            }).ToList());
    }

    private static ReportData Reorder(IQueryable<Product> products, string category) => new("Sugerencias de reposición", category,
        ["Código", "Producto", "Stock", "Mínimo", "Unidades sugeridas"], products.Where(p => p.Active && p.Inventory.OnHand <= p.Inventory.Minimum)
            .OrderBy(p => p.Inventory.OnHand).ThenBy(p => p.Name).Select(p => new { p.Code, p.Name, p.Inventory.OnHand, p.Inventory.Minimum }).ToList()
            .Select(p => new object?[] { p.Code, p.Name, p.OnHand, p.Minimum, Math.Max(1, (long)p.Minimum + 1 - p.OnHand) }).ToList());

    private sealed record LineAmount(long SaleId, int ProductId, string Code, string Name, int Quantity, long Net, long Cost);

    private static List<LineAmount> Amounts(StoreContext db, IQueryable<Sale> query, int? category)
    {
        var source = query.SelectMany(s => s.Lines, (s, line) => new { s.Id, LineId = line.Id, line.ProductId, line.CategoryId,
            line.Code, line.Name, line.Quantity, line.UnitPrice, line.UnitCost, s.Subtotal, s.Discount }).OrderBy(row => row.Id).ThenBy(row => row.LineId).ToList();
        var currentCategories = category.HasValue ? db.Products.Select(p => new { p.Id, p.CategoryId }).ToDictionary(p => p.Id, p => p.CategoryId) : null;
        var result = new List<LineAmount>(source.Count);
        foreach (var sale in source.GroupBy(row => row.Id))
        {
            long total = 0;
            long assigned = 0;
            foreach (var line in sale)
            {
                var gross = line.Quantity * line.UnitPrice;
                total += gross;
                var next = (long)((Int128)total * line.Discount / line.Subtotal);
                if (!category.HasValue || (line.CategoryId ?? currentCategories!.GetValueOrDefault(line.ProductId)) == category)
                    result.Add(new LineAmount(line.Id, line.ProductId, line.Code, line.Name, line.Quantity, gross - (next - assigned), line.Quantity * line.UnitCost));
                assigned = next;
            }
        }
        return result;
    }

    private static decimal Money(long cents) => cents / 100m;
    private static DateTime? Local(DateTime? utc) => utc.HasValue ? DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime() : null;
    private static string Status(DispatchStatus status) => status switch
    {
        DispatchStatus.EnPreparación => "En preparación", DispatchStatus.ListoParaRecoger => "Listo para recoger", _ => status.ToString()
    };
}
