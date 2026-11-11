using Comercium.Application;
using Comercium.Domain;
using Comercium.Infrastructure.Persistence;
using Comercium.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

    public void Dispose()
    {
        using var db = database.Open();
        SqliteConnection.ClearPool((SqliteConnection)db.Database.GetDbConnection());
        foreach (var file in Directory.EnumerateFiles(folder))
            File.Delete(file);
        Directory.Delete(folder);
    }
}
