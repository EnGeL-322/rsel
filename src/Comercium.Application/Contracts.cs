using Comercium.Domain;

namespace Comercium.Application;

public sealed record Session(int UserId, string Name, Permission Permissions);

public interface IAccounts
{
    bool NeedsSetup();
    Session CreateOwner(string name, string login, string password);
    Session SignIn(string login, string password);
    void ChangePassword(Session session, string current, string replacement);
}

public interface IBackups
{
    void Create(Session session, string destination);
    string Restore(Session session, string source);
}

public sealed record ProductDraft(int Id, string Code, string Name, string Description, string Category,
    string Unit, string Location, long Cost, long Retail, long Wholesale, int Minimum, byte[]? Photo, bool Active);

public sealed record ProductRow(int Id, string Code, string Name, string Category, string Unit,
    string Location, long Retail, long Wholesale, int Stock, int Minimum, bool Active);

public sealed record MovementRow(DateTime AtUtc, MovementKind Kind, int Change, int Balance, string Reason, string User);
public sealed record PriceRow(DateTime AtUtc, long Cost, long Retail, long Wholesale);

public interface ICatalog
{
    IReadOnlyList<ProductRow> Search(Session session, string text, string? category = null, bool activeOnly = false);
    ProductDraft Load(Session session, int id);
    int Save(Session session, ProductDraft product);
    void Move(Session session, int productId, Guid operationId, MovementKind kind, int quantity, string reason);
    IReadOnlyList<MovementRow> Movements(Session session, int productId);
    IReadOnlyList<PriceRow> Prices(Session session, int productId);
}

public sealed record CartLine(int ProductId, int Quantity, bool Wholesale, long ExpectedPrice);
public sealed record Checkout(Guid OperationId, IReadOnlyList<CartLine> Lines, long Discount, PaymentMethod Method, long Received, int? CustomerId = null);
public sealed record SaleRow(long Id, DateTime AtUtc, long Total, PaymentMethod Method, bool Voided, string Customer = "");
public sealed record ReceiptLine(string Code, string Name, int Quantity, long UnitPrice);
public sealed record Receipt(long Id, DateTime AtUtc, string Cashier, long Subtotal, long Discount, long Total,
    PaymentMethod Method, long Received, bool Voided, IReadOnlyList<ReceiptLine> Lines, string Customer = "");

public sealed record DispatchRow(long Id, DateTime AtUtc, DispatchStatus Status, int Verified, int Total);
public sealed record PickLine(long Id, string Code, string Name, string Location, int Quantity, bool Verified);
public sealed record DispatchOrder(long Id, DispatchStatus Status, DateTime? StartedUtc, string? StartedBy,
    DateTime? ReadyUtc, string? ReadyBy, DateTime? DeliveredUtc, string? DeliveredBy, IReadOnlyList<PickLine> Lines);

public interface ISales
{
    long Confirm(Session session, Checkout checkout);
    void Void(Session session, long id, string reason);
    IReadOnlyList<SaleRow> Recent(Session session);
    IReadOnlyList<SaleRow> Search(Session session, DateOnly? from, DateOnly? through, string term);
    Receipt Receipt(Session session, long id);
}

public interface IDispatch
{
    IReadOnlyList<DispatchRow> List(Session session, bool includeCompleted);
    DispatchOrder Load(Session session, long id);
    void Verify(Session session, long id, long lineId, bool verified);
    void Advance(Session session, long id, DispatchStatus expected);
}

public sealed record SalesDay(DateOnly Day, long Amount);
public sealed record BestSeller(string Name, int Quantity);
public sealed record Dashboard(long Today, long Month, long Margin, int Products, int LowStock, int OutOfStock,
    int PendingDispatches, IReadOnlyList<SalesDay> Week, IReadOnlyList<BestSeller> BestSellers, IReadOnlyList<PurchaseRow> RecentPurchases);

public interface IDashboard
{
    Dashboard Read(Session session);
}

public sealed record SupplierRow(int Id, string Name, string Phone);
public sealed record PurchaseItem(int ProductId, int Quantity, long UnitCost);
public sealed record ReceivePurchase(Guid OperationId, int SupplierId, string Reference, IReadOnlyList<PurchaseItem> Items);
public sealed record PurchaseRow(long Id, DateTime AtUtc, string Supplier, string Reference, long Total, long Paid)
{
    public long Outstanding => Total - Paid;
}
public sealed record PurchaseDetail(string Code, string Product, int Quantity, long UnitCost);
public sealed record PurchasePaymentRow(DateTime AtUtc, PaymentMethod Method, long Amount);
public sealed record PurchaseReceipt(IReadOnlyList<PurchaseDetail> Items, IReadOnlyList<PurchasePaymentRow> Payments);
public sealed record SupplierCost(string Supplier, DateTime AtUtc, long UnitCost);

public interface IPurchases
{
    IReadOnlyList<SupplierRow> Suppliers(Session session);
    int SaveSupplier(Session session, SupplierRow supplier);
    long Receive(Session session, ReceivePurchase purchase);
    void Pay(Session session, long purchaseId, Guid operationId, long amount, PaymentMethod method);
    IReadOnlyList<PurchaseRow> History(Session session, bool outstandingOnly);
    PurchaseReceipt Detail(Session session, long purchaseId);
    IReadOnlyList<SupplierCost> Compare(Session session, int productId);
}
