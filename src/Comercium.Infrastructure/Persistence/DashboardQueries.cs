using Comercium.Application;
using Comercium.Domain;

namespace Comercium.Infrastructure.Persistence;

public sealed class DashboardQueries(StoreDatabase database) : IDashboard
{
    public Dashboard Read(Session session)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.ViewDashboard);
        var today = DateTime.Today;
        var day = today.ToUniversalTime();
        var end = today.AddDays(1).ToUniversalTime();
        var month = today.AddDays(1 - today.Day).ToUniversalTime();
        var sales = db.Sales.Where(sale => sale.VoidedUtc == null && sale.OccurredUtc < end);
        var monthSales = sales.Where(sale => sale.OccurredUtc >= month);
        var weekStart = today.AddDays(-6).ToUniversalTime();
        var weekAmounts = sales.Where(sale => sale.OccurredUtc >= weekStart)
            .Select(sale => new { sale.OccurredUtc, sale.Total }).ToList()
            .ToLookup(sale => DateOnly.FromDateTime(DateTime.SpecifyKind(sale.OccurredUtc, DateTimeKind.Utc).ToLocalTime()));
        var week = Enumerable.Range(-6, 7).Select(offset => DateOnly.FromDateTime(today.AddDays(offset)))
            .Select(date => new SalesDay(date, weekAmounts[date].Sum(sale => sale.Total))).ToList();
        var best = monthSales.SelectMany(sale => sale.Lines).GroupBy(line => line.ProductId)
            .Select(group => new { Id = group.Key, Quantity = group.Sum(line => line.Quantity) });
        var bestSellers = (from row in best
                           join product in db.Products on row.Id equals product.Id
                           orderby row.Quantity descending, product.Name
                           select new BestSeller(product.Name, row.Quantity)).Take(5).ToList();

        var active = db.Products.Where(product => product.Active);
        return new Dashboard(sales.Where(sale => sale.OccurredUtc >= day).Sum(sale => sale.Total),
            monthSales.Sum(sale => sale.Total), monthSales.Sum(sale => sale.Total - sale.Cost), active.Count(),
            active.Count(product => product.Inventory.OnHand > 0 && product.Inventory.OnHand <= product.Inventory.Minimum),
            active.Count(product => product.Inventory.OnHand == 0),
            db.Dispatches.Count(order => order.Status != DispatchStatus.Entregado && order.Status != DispatchStatus.Cancelado),
            week, bestSellers, db.Purchases.OrderByDescending(row => row.Id).Take(5).Select(PurchaseRegister.Summary).ToList());
    }
}
