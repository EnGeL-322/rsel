using System.Linq.Expressions;
using Comercium.Application;
using Comercium.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Persistence;

public sealed class PurchaseRegister(StoreDatabase database) : IPurchases
{
    internal static readonly Expression<Func<Purchase, PurchaseRow>> Summary = purchase => new PurchaseRow(purchase.Id,
        purchase.AtUtc, purchase.Supplier.Name, purchase.Reference, purchase.Total, purchase.Payments.Sum(payment => payment.Amount));

    public IReadOnlyList<SupplierRow> Suppliers(Session session)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Buy);
        return db.Suppliers.OrderBy(row => row.Name).Select(row => new SupplierRow(row.Id, row.Name, row.Phone)).ToList();
    }

    public int SaveSupplier(Session session, SupplierRow draft)
    {
        var name = StoreAccess.Required(draft.Name, "El nombre del proveedor", 120);
        var phone = draft.Phone.Trim();
        if (phone.Length > 40)
            throw new BusinessException("Revisa el teléfono del proveedor.");
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Buy);
        var key = name.ToUpperInvariant();
        if (db.Suppliers.Any(supplier => supplier.Id != draft.Id && supplier.NormalizedName == key))
            throw new BusinessException("Ya existe un proveedor con ese nombre.");
        var supplier = draft.Id == 0 ? new Supplier() : db.Suppliers.Find(draft.Id)
            ?? throw new BusinessException("El proveedor seleccionado no existe.");
        supplier.Name = name;
        supplier.NormalizedName = key;
        supplier.Phone = phone;
        if (draft.Id == 0)
            db.Suppliers.Add(supplier);
        StoreAccess.Log(db, session.UserId, DateTime.UtcNow, "Proveedor guardado", name);
        db.SaveChanges();
        transaction.Commit();
        return supplier.Id;
    }

    public long Receive(Session session, ReceivePurchase request)
    {
        if (request.OperationId == Guid.Empty || request.Items.Count is < 1 or > 200
            || request.Items.Any(item => item.Quantity is < 1 or > 1_000_000 || item.UnitCost is < 1 or > 100_000_000)
            || request.Items.Select(item => item.ProductId).Distinct().Count() != request.Items.Count)
            throw new BusinessException("Ingresa productos distintos, cada uno con cantidad y costo válidos.");
        var reference = request.Reference.Trim();
        if (reference.Length > 100)
            throw new BusinessException("La referencia debe tener hasta 100 caracteres.");
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Buy);
        var existing = db.Purchases.SingleOrDefault(row => row.OperationId == request.OperationId);
        if (existing is not null)
        {
            if (existing.UserId != session.UserId)
                throw new BusinessException("La operación pertenece a otra cuenta.");
            return existing.Id;
        }

        if (!db.Suppliers.Any(row => row.Id == request.SupplierId))
            throw new BusinessException("Selecciona el proveedor de la compra.");
        var now = DateTime.UtcNow;
        var purchase = new Purchase { OperationId = request.OperationId, SupplierId = request.SupplierId,
            Reference = reference, AtUtc = now, UserId = session.UserId };
        foreach (var item in request.Items)
        {
            var product = StoreAccess.FindProduct(db, item.ProductId);
            if (!product.Active)
                throw new BusinessException($"{product.Name} está desactivado.");
            purchase.Lines.Add(new PurchaseLine { ProductId = product.Id, Quantity = item.Quantity, UnitCost = item.UnitCost });
            purchase.Total += item.UnitCost * item.Quantity;
            StoreAccess.Move(db, product, session.UserId, request.OperationId, now, MovementKind.Compra, item.Quantity, "Compra recibida");
            if (product.Cost == item.UnitCost)
                continue;
            product.Cost = item.UnitCost;
            db.Prices.Add(new PriceEntry { ProductId = product.Id, UserId = session.UserId, OccurredUtc = now,
                Cost = item.UnitCost, Retail = product.Retail, Wholesale = product.Wholesale });
        }

        db.Purchases.Add(purchase);
        db.SaveChanges();
        StoreAccess.Log(db, session.UserId, now, "Compra registrada", $"Compra {purchase.Id} · Proveedor {purchase.SupplierId}");
        db.SaveChanges();
        transaction.Commit();
        return purchase.Id;
    }

    public void Pay(Session session, long purchaseId, Guid operationId, long amount, PaymentMethod method)
    {
        if (operationId == Guid.Empty || amount <= 0 || !Enum.IsDefined(method))
            throw new BusinessException("Revisa el importe y el medio de pago.");
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Buy);
        if (db.PurchasePayments.Any(payment => payment.OperationId == operationId && payment.PurchaseId == purchaseId))
            return;
        var purchase = db.Purchases.Include(row => row.Payments).SingleOrDefault(row => row.Id == purchaseId)
            ?? throw new BusinessException("Selecciona una compra registrada.");
        if (amount > purchase.Total - purchase.Payments.Sum(payment => payment.Amount))
            throw new BusinessException("El importe supera el saldo pendiente de la compra.");

        var now = DateTime.UtcNow;
        var payment = new PurchasePayment { OperationId = operationId, Amount = amount, Method = method, AtUtc = now, UserId = session.UserId };
        purchase.Payments.Add(payment);
        db.SaveChanges();
        if (method == PaymentMethod.Efectivo)
            CashRegister.Post(db, CashRegister.Active(db), session, now, CashKind.Proveedor, -amount, payment.Id, $"Pago de compra {purchaseId}");
        StoreAccess.Log(db, session.UserId, now, "Pago de compra registrado", $"Compra {purchaseId}");
        db.SaveChanges();
        transaction.Commit();
    }

    public IReadOnlyList<PurchaseRow> History(Session session, bool outstandingOnly)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Buy);
        var query = outstandingOnly ? db.Purchases.Where(row => row.Payments.Sum(payment => payment.Amount) < row.Total) : db.Purchases;
        return query.OrderByDescending(row => row.Id).Select(Summary).ToList();
    }

    public PurchaseReceipt Detail(Session session, long purchaseId)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Buy);
        var query = db.Purchases.Where(row => row.Id == purchaseId);
        return new PurchaseReceipt(query.SelectMany(row => row.Lines).OrderBy(line => line.Id)
            .Select(line => new PurchaseDetail(line.Product.Code, line.Product.Name, line.Quantity, line.UnitCost)).ToList(),
            query.SelectMany(row => row.Payments).OrderBy(payment => payment.Id)
                .Select(payment => new PurchasePaymentRow(payment.AtUtc, payment.Method, payment.Amount)).ToList());
    }

    public IReadOnlyList<SupplierCost> Compare(Session session, int productId)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Buy);
        return db.Purchases.Where(row => row.Lines.Any(line => line.ProductId == productId)).OrderByDescending(row => row.Id)
            .Select(row => new { row.SupplierId, Supplier = row.Supplier.Name, row.AtUtc,
                Cost = row.Lines.Where(line => line.ProductId == productId).Select(line => line.UnitCost).First() })
            .ToList().DistinctBy(row => row.SupplierId).OrderBy(row => row.Cost)
            .Select(row => new SupplierCost(row.Supplier, row.AtUtc, row.Cost)).ToList();
    }
}
