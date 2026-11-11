using Comercium.Application;
using Comercium.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Persistence;

public sealed class SalesRegister(StoreDatabase database) : ISales
{
    public long Confirm(Session session, Checkout checkout)
    {
        if (checkout.OperationId == Guid.Empty || checkout.Lines.Count is < 1 or > 200
            || checkout.Lines.Any(line => line.Quantity is < 1 or > 1_000_000))
            throw new BusinessException("Agrega de 1 a 200 líneas con cantidades válidas.");
        if (!Enum.IsDefined(checkout.Method) || checkout.Discount < 0)
            throw new BusinessException("Revisa el método de pago y el descuento.");

        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Sell);
        if (checkout.Discount > 0)
            StoreAccess.Demand(db, session, Permission.ApplyDiscount);
        var existing = db.Sales.SingleOrDefault(sale => sale.OperationId == checkout.OperationId);
        if (existing is not null)
        {
            if (existing.UserId != session.UserId)
                throw new BusinessException("La operación pertenece a otra cuenta.");
            return existing.Id;
        }

        var now = DateTime.UtcNow;
        var ids = checkout.Lines.Select(line => line.ProductId).Distinct().ToArray();
        var products = db.Products.Include(product => product.Inventory).Where(product => ids.Contains(product.Id))
            .ToDictionary(product => product.Id);
        var sale = new Sale { OperationId = checkout.OperationId, UserId = session.UserId, OccurredUtc = now, Dispatch = new Dispatch() };

        foreach (var requested in checkout.Lines)
        {
            if (!products.TryGetValue(requested.ProductId, out var product) || !product.Active)
                throw new BusinessException("El carrito contiene un producto que ya no está activo.");
            var price = requested.Wholesale ? product.Wholesale : product.Retail;
            if (price != requested.ExpectedPrice)
                throw new BusinessException($"El precio de {product.Name} cambió. Actualiza el carrito antes de cobrar.");

            var line = new SaleLine { ProductId = product.Id, Code = product.Code, Name = product.Name,
                Location = product.Location, Quantity = requested.Quantity, UnitPrice = price, UnitCost = product.Cost };
            sale.Lines.Add(line);
            sale.Dispatch.Lines.Add(new DispatchLine { SaleLine = line });
            sale.Subtotal += line.UnitPrice * line.Quantity;
            sale.Cost += line.UnitCost * line.Quantity;
        }

        if (checkout.Discount > sale.Subtotal)
            throw new BusinessException("El descuento no puede superar el subtotal.");
        sale.Discount = checkout.Discount;
        sale.Total = sale.Subtotal - sale.Discount;
        var received = checkout.Method == PaymentMethod.Efectivo ? checkout.Received : sale.Total;
        if (received < sale.Total)
            throw new BusinessException("El efectivo recibido es menor al total de la venta.");
        sale.Payment = new Payment { Method = checkout.Method, Amount = sale.Total, Received = received };
        foreach (var group in sale.Lines.GroupBy(line => line.ProductId))
            StoreAccess.Move(db, products[group.Key], session.UserId, checkout.OperationId, now, MovementKind.Venta,
                -group.Sum(line => line.Quantity), "Venta en tienda");

        db.Sales.Add(sale);
        db.SaveChanges();
        StoreAccess.Log(db, session.UserId, now, "Venta confirmada", $"Venta {sale.Id}");
        db.SaveChanges();
        transaction.Commit();
        return sale.Id;
    }

    public void Void(Session session, long id, string reason)
    {
        reason = StoreAccess.Required(reason, "El motivo", 250);
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.VoidSale);
        var sale = db.Sales.Include(sale => sale.Dispatch).Include(sale => sale.Payment).Include(sale => sale.Lines)
            .SingleOrDefault(sale => sale.Id == id) ?? throw new BusinessException("La venta no existe.");
        if (sale.VoidedUtc.HasValue)
            return;
        if (sale.Dispatch.Status == DispatchStatus.Entregado)
            throw new BusinessException("Esta venta ya se entregó. Su devolución requiere otro proceso.");

        var now = DateTime.UtcNow;
        var operation = Guid.NewGuid();
        foreach (var group in sale.Lines.GroupBy(line => line.ProductId))
            StoreAccess.Move(db, StoreAccess.FindProduct(db, group.Key), session.UserId, operation, now,
                MovementKind.Anulación, group.Sum(line => line.Quantity), $"Venta {id} · {reason}");
        sale.VoidedUtc = now;
        sale.VoidedById = session.UserId;
        sale.VoidReason = reason;
        sale.Payment.ReversedUtc = now;
        sale.Dispatch.Status = DispatchStatus.Cancelado;
        StoreAccess.Log(db, session.UserId, now, "Venta anulada", $"Venta {id} · {reason}");
        db.SaveChanges();
        transaction.Commit();
    }

    public IReadOnlyList<SaleRow> Recent(Session session)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Sell | Permission.VoidSale);
        return db.Sales.OrderByDescending(sale => sale.Id).Take(200)
            .Select(sale => new SaleRow(sale.Id, sale.OccurredUtc, sale.Total, sale.Payment.Method, sale.VoidedUtc.HasValue)).ToList();
    }

    public Receipt Receipt(Session session, long id)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Sell | Permission.VoidSale);
        var sale = db.Sales.Include(sale => sale.Lines).Include(sale => sale.Payment).SingleOrDefault(sale => sale.Id == id)
            ?? throw new BusinessException("La venta no existe.");
        var cashier = db.Users.Where(user => user.Id == sale.UserId).Select(user => user.Name).Single();
        return new Receipt(sale.Id, sale.OccurredUtc, cashier, sale.Subtotal, sale.Discount, sale.Total, sale.Payment.Method,
            sale.Payment.Received, sale.VoidedUtc.HasValue, sale.Lines.OrderBy(line => line.Id)
                .Select(line => new ReceiptLine(line.Code, line.Name, line.Quantity, line.UnitPrice)).ToList());
    }
}
