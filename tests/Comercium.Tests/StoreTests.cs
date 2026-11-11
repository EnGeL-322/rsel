using Comercium.Application;
using Comercium.Domain;
using Comercium.Infrastructure.Persistence;
using Comercium.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Comercium.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "Comercium.Tests", Guid.NewGuid().ToString("N"));
    private readonly StoreDatabase database;
    private readonly Accounts accounts;
    private readonly Catalog catalog;
    private readonly SalesRegister sales;
    private readonly DispatchRegister dispatch;
    private readonly Session owner;

    public StoreTests()
    {
        Directory.CreateDirectory(folder);
        database = new StoreDatabase(Path.Combine(folder, "store.db"));
        database.Migrate();
        accounts = new Accounts(database);
        catalog = new Catalog(database);
        sales = new SalesRegister(database);
        dispatch = new DispatchRegister(database);
        owner = accounts.CreateOwner("Prueba", "propietaria", "Clave exclusiva de prueba");
    }

    [Fact]
    public void MigrationAndAccountPersistAcrossConnections()
    {
        database.Migrate();
        Assert.False(accounts.NeedsSetup());
        Assert.Throws<BusinessException>(() => accounts.CreateOwner("Otra", "otra", "Otra clave de prueba"));
        Assert.Equal(owner, accounts.SignIn(" Propietaria ", "Clave exclusiva de prueba"));
        accounts.ChangePassword(owner, "Clave exclusiva de prueba", "Contraseña nueva de prueba");
        Assert.Throws<BusinessException>(() => accounts.SignIn("propietaria", "Clave exclusiva de prueba"));
        Assert.Equal(owner, accounts.SignIn("propietaria", "Contraseña nueva de prueba"));
        using var db = database.Open();
        Assert.StartsWith("pbkdf2-sha256$", db.Users.Single().PasswordHash);
        Assert.Equal(2, db.Audit.Count());
    }

    [Fact]
    public void ProductCodesAreUniqueAndHistoricalPricesSurviveEdits()
    {
        var id = AddProduct();
        var product = catalog.Load(owner, id);
        Assert.Throws<BusinessException>(() => catalog.Save(owner, product with { Id = 0, Code = " pl-1 " }));
        catalog.Save(owner, product with { Retail = 1100, Active = false });
        Assert.Empty(catalog.Search(owner, "", activeOnly: true));
        Assert.Equal(new long[] { 1100, 1000 }, catalog.Prices(owner, id).Select(row => row.Retail));
        Assert.Equal("Estante A", Assert.Single(catalog.Search(owner, "PL-1", "Cocina")).Location);
    }

    [Fact]
    public void InventoryRejectsNegativeStockAndReplaysMovementOnlyOnce()
    {
        var id = AddProduct();
        var operation = Guid.NewGuid();
        catalog.Move(owner, id, operation, MovementKind.Entrada, 5, "Conteo inicial");
        catalog.Move(owner, id, operation, MovementKind.Entrada, 5, "Conteo inicial");
        Assert.Throws<BusinessException>(() => catalog.Move(owner, id, Guid.NewGuid(), MovementKind.Salida, 6, "Merma"));
        catalog.Move(owner, id, Guid.NewGuid(), MovementKind.Ajuste, 3, "Conteo físico");
        Assert.Equal(3, Assert.Single(catalog.Search(owner, "")).Stock);
        Assert.Equal(new[] { -2, 5 }, catalog.Movements(owner, id).Select(row => row.Change));
    }

    [Fact]
    public void SaleAndPaymentDeductStockExactlyOnceAndFeedDashboard()
    {
        var id = AddProduct(6);
        var checkout = new Checkout(Guid.NewGuid(), [new(id, 2, false, 1000), new(id, 1, true, 800)], 100, PaymentMethod.Efectivo, 3000);
        var saleId = sales.Confirm(owner, checkout);
        Assert.Equal(saleId, sales.Confirm(owner, checkout));
        Assert.Equal(3, Assert.Single(catalog.Search(owner, "")).Stock);
        Assert.Single(catalog.Movements(owner, id), row => row.Kind == MovementKind.Venta);
        var receipt = sales.Receipt(owner, saleId);
        Assert.Equal(2700, receipt.Total);
        Assert.Equal(300, receipt.Received - receipt.Total);
        var data = new DashboardQueries(database).Read(owner);
        Assert.Equal(2700, data.Today);
        Assert.Equal(1500, data.Margin);
        Assert.Equal(3, Assert.Single(data.BestSellers).Quantity);
        Assert.Equal(1, data.PendingDispatches);
        Assert.Equal(2, Assert.Single(dispatch.List(owner, false)).Total);
    }

    [Fact]
    public void BackupRestoreKeepsARecoveryCopyAndRejectsInvalidFiles()
    {
        AddProduct(8);
        var service = new StoreBackups(database);
        var invalid = Path.Combine(folder, "invalid.comercium");
        File.WriteAllText(invalid, "archivo de prueba");
        Assert.Throws<BusinessException>(() => service.Restore(owner, invalid));
        var backup = Path.Combine(folder, "export.comercium");
        service.Create(owner, backup);
        AddProduct(2, "PL-2");
        var recovery = service.Restore(owner, backup);
        Assert.Single(catalog.Search(owner, ""));
        Assert.True(File.Exists(recovery));
        service.Restore(owner, recovery);
        Assert.Equal(2, catalog.Search(owner, "").Count);
    }

    [Fact]
    public void PurchaseReceiptUpdatesStockOnceAndTracksPartialPaymentsAndSupplierCost()
    {
        var id = AddProduct(3);
        var purchases = new PurchaseRegister(database);
        var first = purchases.SaveSupplier(owner, new SupplierRow(0, "Proveedor A", ""));
        var second = purchases.SaveSupplier(owner, new SupplierRow(0, "Proveedor B", ""));
        var request = new ReceivePurchase(Guid.NewGuid(), first, "Factura 1", [new PurchaseItem(id, 4, 500)]);
        var purchaseId = purchases.Receive(owner, request);
        Assert.Equal(purchaseId, purchases.Receive(owner, request));
        purchases.Receive(owner, new ReceivePurchase(Guid.NewGuid(), second, "Factura 2", [new PurchaseItem(id, 1, 550)]));
        Assert.Equal(8, Assert.Single(catalog.Search(owner, "")).Stock);
        Assert.Equal(550, catalog.Load(owner, id).Cost);
        Assert.Equal(new long[] { 500, 550 }, purchases.Compare(owner, id).Select(row => row.UnitCost));
        var payment = Guid.NewGuid();
        purchases.Pay(owner, purchaseId, payment, 700, PaymentMethod.Transferencia);
        purchases.Pay(owner, purchaseId, payment, 700, PaymentMethod.Transferencia);
        Assert.Throws<BusinessException>(() => purchases.Pay(owner, purchaseId, Guid.NewGuid(), 1301, PaymentMethod.Efectivo));
        Assert.Equal(1300, purchases.History(owner, true).Single(row => row.Id == purchaseId).Outstanding);
        Assert.Equal(700, Assert.Single(purchases.Detail(owner, purchaseId).Payments).Amount);
        purchases.Pay(owner, purchaseId, Guid.NewGuid(), 1300, PaymentMethod.Efectivo);
        Assert.DoesNotContain(purchases.History(owner, true), row => row.Id == purchaseId);
        Assert.Equal(2, new DashboardQueries(database).Read(owner).RecentPurchases.Count);
    }

    [Fact]
    public void FailedSaleLeavesStockPaymentAndDispatchUntouched()
    {
        var first = AddProduct(6);
        var second = AddProduct(1, "PL-2");
        var checkout = new Checkout(Guid.NewGuid(), [new(first, 2, false, 1000), new(second, 2, false, 1000)],
            0, PaymentMethod.Tarjeta, 0);
        Assert.Throws<BusinessException>(() => sales.Confirm(owner, checkout));
        Assert.Empty(sales.Recent(owner));
        Assert.Empty(dispatch.List(owner, true));
        Assert.Equal(6, catalog.Search(owner, "PL-1").Single().Stock);
        Assert.Single(catalog.Movements(owner, first));
    }

    [Fact]
    public void DispatchRequiresEveryLineAndRecordsWhoDelivered()
    {
        var first = AddProduct(6);
        var second = AddProduct(3, "PL-2");
        var sale = sales.Confirm(owner, new Checkout(Guid.NewGuid(), [new(first, 2, false, 1000), new(second, 1, false, 1000)],
            0, PaymentMethod.Transferencia, 0));
        Assert.Throws<BusinessException>(() => dispatch.Advance(owner, sale, DispatchStatus.ListoParaRecoger));
        dispatch.Advance(owner, sale, DispatchStatus.Pendiente);
        var order = dispatch.Load(owner, sale);
        dispatch.Verify(owner, sale, order.Lines[0].Id, true);
        Assert.Throws<BusinessException>(() => dispatch.Advance(owner, sale, DispatchStatus.EnPreparación));
        dispatch.Verify(owner, sale, order.Lines[1].Id, true);
        dispatch.Advance(owner, sale, DispatchStatus.EnPreparación);
        dispatch.Advance(owner, sale, DispatchStatus.ListoParaRecoger);
        var delivered = dispatch.Load(owner, sale);
        Assert.Equal(DispatchStatus.Entregado, delivered.Status);
        Assert.Equal(owner.Name, delivered.DeliveredBy);
        Assert.NotNull(delivered.DeliveredUtc);
        Assert.Empty(dispatch.List(owner, false));
        Assert.Throws<BusinessException>(() => sales.Void(owner, sale, "Devolución posterior"));
    }

    [Fact]
    public void VoidingRestoresStockAndRetainsReceiptAndOrderHistory()
    {
        var id = AddProduct(6);
        var sale = sales.Confirm(owner, new Checkout(Guid.NewGuid(), [new(id, 2, false, 1000)], 0, PaymentMethod.Tarjeta, 0));
        sales.Void(owner, sale, "Pedido cancelado en tienda");
        sales.Void(owner, sale, "Segundo intento");
        Assert.Equal(6, Assert.Single(catalog.Search(owner, "")).Stock);
        Assert.True(sales.Receipt(owner, sale).Voided);
        Assert.Equal(DispatchStatus.Cancelado, dispatch.Load(owner, sale).Status);
        Assert.Equal(0, new DashboardQueries(database).Read(owner).Today);
        using var db = database.Open();
        Assert.NotNull(db.Sales.Include(row => row.Payment).Single().Payment.ReversedUtc);
        Assert.Single(db.Movements.Where(row => row.Kind == MovementKind.Anulación));
    }

    private int AddProduct(int stock = 0, string code = "PL-1")
    {
        var id = catalog.Save(owner, new ProductDraft(0, code, "Plato de prueba", "", "Cocina", "Unidad",
            "Estante A", 400, 1000, 800, 2, null, true));
        if (stock > 0)
            catalog.Move(owner, id, Guid.NewGuid(), MovementKind.Entrada, stock, "Conteo inicial");
        return id;
    }

    [Fact]
    public void CustomerHistoryRetainsOriginalNameAndSkipsVoidsInItsTotals()
    {
        var service = new Customers(database);
        var customer = service.Save(owner, new CustomerCard(0, "María Pérez", "12345678", "999001122", true, true));
        Assert.Throws<BusinessException>(() => service.Save(owner, new CustomerCard(0, "Duplicado", "12345678", "", false, true)));
        var product = AddProduct(3);
        var id = sales.Confirm(owner, new Checkout(Guid.NewGuid(), [new(product, 1, true, 800)], 0, PaymentMethod.Transferencia, 0, customer));
        service.Save(owner, new CustomerCard(customer, "María Torres", "12345678", "", true, false));
        Assert.Equal("María Pérez", sales.Receipt(owner, id).Customer);
        Assert.Empty(service.ForSale(owner));
        Assert.Equal(800, Assert.Single(service.Search(owner, "maría")).Spent);
        var day = DateOnly.FromDateTime(DateTime.Today);
        Assert.Equal(id, Assert.Single(sales.Search(owner, day, day, "12345678")).Id);
        Assert.Equal(id, Assert.Single(sales.Search(owner, day, day, "MARÍA PÉREZ")).Id);
        Assert.Throws<BusinessException>(() => sales.Confirm(owner, new Checkout(Guid.NewGuid(), [new(product, 1, false, 1000)], 0, PaymentMethod.Tarjeta, 0, customer)));
        sales.Void(owner, id, "Prueba de anulación");
        Assert.Equal(0, Assert.Single(service.Search(owner, "")).Purchases);
        Assert.True(Assert.Single(service.History(owner, customer)).Voided);
    }

    [Fact]
    public void CashReconcilesSalesExpensesReversalsAndSupplierPayments()
    {
        var cash = new CashRegister(database);
        var purchases = new PurchaseRegister(database);
        var product = AddProduct(5);
        var supplier = purchases.SaveSupplier(owner, new SupplierRow(0, "Distribuidora", ""));
        var purchase = purchases.Receive(owner, new ReceivePurchase(Guid.NewGuid(), supplier, "", [new(product, 3, 400)]));
        cash.Open(owner, 2000);
        Assert.Throws<BusinessException>(() => cash.Open(owner, 0));
        var shift = cash.Read(owner).Open!.Id;
        var checkout = new Checkout(Guid.NewGuid(), [new(product, 1, false, 1000)], 0, PaymentMethod.Efectivo, 1500);
        var id = sales.Confirm(owner, checkout);
        Assert.Equal(id, sales.Confirm(owner, checkout));
        sales.Confirm(owner, checkout with { OperationId = Guid.NewGuid(), Method = PaymentMethod.Tarjeta });
        purchases.Pay(owner, purchase, Guid.NewGuid(), 400, PaymentMethod.Efectivo);
        var movement = Guid.NewGuid();
        cash.Move(owner, shift, movement, CashKind.Gasto, 125, "Servicio");
        cash.Move(owner, shift, movement, CashKind.Gasto, 125, "Servicio");
        Assert.Equal(2475, cash.Read(owner).Open!.Expected);
        cash.Close(owner, shift, 2400);
        Assert.Equal(-75, Assert.Single(cash.Read(owner).Shifts).Difference);
        Assert.Throws<BusinessException>(() => sales.Void(owner, id, "Caja cerrada"));
        Assert.False(sales.Receipt(owner, id).Voided);
        cash.Open(owner, 1500);
        sales.Void(owner, id, "Efectivo devuelto");
        sales.Void(owner, id, "Reintento");
        var current = cash.Read(owner).Open!;
        Assert.Equal(500, current.Expected);
        Assert.Equal(CashKind.Reversión, Assert.Single(cash.Entries(owner, current.Id)).Kind);
    }

    [Fact]
    public void ReportsFilterCategoriesAndAllocateEveryCentOfTheDiscount()
    {
        var first = AddProduct(4);
        var second = AddProduct(4, "PL-2");
        catalog.Save(owner, catalog.Load(owner, second) with { Category = "Mesa" });
        sales.Confirm(owner, new Checkout(Guid.NewGuid(), [new(first, 1, false, 1000), new(second, 1, false, 1000)], 1, PaymentMethod.Tarjeta, 0));
        catalog.Save(owner, catalog.Load(owner, first) with { Category = "Mesa" });
        var service = new Reporting(database);
        var day = DateOnly.FromDateTime(DateTime.Today);
        var categories = service.Categories(owner).Where(row => row.Id.HasValue).ToList();
        var cents = categories.Select(category => Assert.IsType<decimal>(Assert.Single(service.Read(owner,
            new ReportFilter(ReportKind.Ventas, day, day, category.Id)).Rows)[5])).ToList();
        Assert.Equal(19.99m, cents.Sum());
        Assert.Equal(1999, service.Compare(owner, day, day).Revenue);
        foreach (var kind in Enum.GetValues<ReportKind>())
        {
            var result = service.Read(owner, new ReportFilter(kind, day, day, categories[0].Id));
            Assert.All(result.Rows, row => Assert.Equal(result.Columns.Count, row.Length));
        }
        Assert.Throws<BusinessException>(() => service.Read(owner, new ReportFilter(ReportKind.Ventas, day, day.AddDays(-1))));
    }

    [Fact]
    public void PlanPersistsFodaGoalsAndReadings()
    {
        var strategy = new Strategy(database);
        var day = DateOnly.FromDateTime(DateTime.Today);
        var goal = new Goal(0, "Mejorar rotación", "Revisar semanalmente", "", "Unidades por mes", "unidades", 45.5m, day.AddMonths(1), false);
        var id = strategy.SaveGoal(owner, goal);
        var profile = new PlanProfile("Atender a familias", "Crecer", "Respeto", "Servicio", "Nuevas líneas", "Espacio", "Competencia");
        strategy.SaveProfile(owner, profile);
        strategy.Measure(owner, id, 30.25m, "Medición inicial");
        strategy.SaveGoal(owner, goal with { Id = id, Completed = true, Evaluation = "Evaluado" });
        Assert.Equal(profile, strategy.Profile(owner));
        Assert.True(Assert.Single(strategy.Goals(owner)).Completed);
        Assert.Equal(30.25m, Assert.Single(strategy.Readings(owner, id)).Value);
    }

    [Fact]
    public void ExistingSalesAndOwnerSurviveUpgradeAndRestoreFromPreviousSchema()
    {
        var priorPath = Path.Combine(folder, "previous.comercium");
        var priorDatabase = new StoreDatabase(priorPath, pooling: false);
        var oldOwner = new Accounts(priorDatabase);
        Session prior;
        using (var db = priorDatabase.Open())
        {
            db.GetService<IMigrator>().Migrate("SupplierPurchases");
            prior = oldOwner.CreateOwner("Cuenta anterior", "anterior", "Clave anterior de prueba");
            db.Database.ExecuteSqlRaw("UPDATE Roles SET Permissions = 511;");
            db.Database.ExecuteSqlInterpolated($"INSERT INTO Sales (OperationId, UserId, OccurredUtc, Subtotal, Discount, Total, Cost) VALUES ({Guid.NewGuid()}, {prior.UserId}, {DateTime.UtcNow}, 1800, 100, 1700, 800);");
            var id = db.Database.SqlQueryRaw<long>("SELECT Id AS Value FROM Sales").Single();
            db.Database.ExecuteSqlInterpolated($"INSERT INTO Payments (SaleId, Method, Amount, Received) VALUES ({id}, 0, 1700, 1700);");
            db.Database.ExecuteSqlInterpolated($"INSERT INTO Dispatches (SaleId, Status) VALUES ({id}, 0);");
        }
        new StoreBackups(database).Restore(owner, priorPath);
        var upgraded = accounts.SignIn("anterior", "Clave anterior de prueba");
        Assert.True(upgraded.Permissions.HasFlag(Permission.Reports | Permission.Finance | Permission.Plan | Permission.Clients));
        var sale = Assert.Single(sales.Recent(upgraded));
        Assert.Equal(1700, sale.Total);
        Assert.Equal("", sale.Customer);
    }

    public void Dispose()
    {
        using var db = database.Open();
        SqliteConnection.ClearPool((SqliteConnection)db.Database.GetDbConnection());
        foreach (var file in Directory.EnumerateFiles(folder))
            File.Delete(file);
        Directory.Delete(folder);
    }
}
