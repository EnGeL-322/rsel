using Comercium.Application;
using Comercium.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Persistence;

public sealed class DispatchRegister(StoreDatabase database) : IDispatch
{
    public IReadOnlyList<DispatchRow> List(Session session, bool includeCompleted)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Dispatch);
        var orders = includeCompleted
            ? db.Sales.OrderByDescending(sale => sale.Id).Take(200)
            : db.Sales.Where(sale => sale.Dispatch.Status != DispatchStatus.Entregado && sale.Dispatch.Status != DispatchStatus.Cancelado)
                .OrderBy(sale => sale.Id);
        return orders.Select(sale => new DispatchRow(sale.Id, sale.OccurredUtc, sale.Dispatch.Status,
            sale.Dispatch.Lines.Count(line => line.VerifiedUtc != null), sale.Dispatch.Lines.Count)).ToList();
    }

    public DispatchOrder Load(Session session, long id)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Dispatch);
        var order = Find(db, id);
        var responsibleIds = new[] { order.StartedById, order.ReadyById, order.DeliveredById };
        var users = db.Users.Where(user => responsibleIds.Contains(user.Id)).ToDictionary(user => user.Id, user => user.Name);
        string? Name(int? userId) => userId.HasValue ? users.GetValueOrDefault(userId.Value) : null;
        return new DispatchOrder(id, order.Status, order.StartedUtc, Name(order.StartedById),
            order.ReadyUtc, Name(order.ReadyById), order.DeliveredUtc, Name(order.DeliveredById),
            order.Lines.OrderBy(line => line.SaleLineId).Select(line => new PickLine(line.SaleLineId,
                line.SaleLine.Code, line.SaleLine.Name, line.SaleLine.Location, line.SaleLine.Quantity, line.VerifiedUtc.HasValue)).ToList());
    }

    public void Verify(Session session, long id, long lineId, bool verified)
    {
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Dispatch);
        var order = Find(db, id);
        if (order.Status != DispatchStatus.EnPreparación)
            throw new BusinessException("Inicia la preparación para verificar los productos.");
        var line = order.Lines.SingleOrDefault(line => line.SaleLineId == lineId)
            ?? throw new BusinessException("Selecciona una línea del pedido.");
        if (line.VerifiedUtc.HasValue == verified)
            return;

        var now = DateTime.UtcNow;
        line.VerifiedUtc = verified ? now : null;
        line.VerifiedById = verified ? session.UserId : null;
        StoreAccess.Log(db, session.UserId, now, verified ? "Producto verificado" : "Verificación retirada",
            $"Pedido {id} · {line.SaleLine.Code} · {line.SaleLine.Quantity} unidades");
        db.SaveChanges();
        transaction.Commit();
    }

    public void Advance(Session session, long id, DispatchStatus expected)
    {
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Dispatch);
        var order = Find(db, id);
        if (order.Status != expected)
            throw new BusinessException("El estado del pedido cambió. Actualiza la lista e inténtalo de nuevo.");
        var now = DateTime.UtcNow;
        order.Advance(session.UserId, now);
        StoreAccess.Log(db, session.UserId, now, "Despacho actualizado", $"Pedido {id} · {expected} → {order.Status}");
        db.SaveChanges();
        transaction.Commit();
    }

    private static Dispatch Find(StoreContext db, long id) => db.Dispatches.Include(order => order.Lines)
        .ThenInclude(line => line.SaleLine).SingleOrDefault(order => order.SaleId == id)
        ?? throw new BusinessException("El pedido no existe.");
}
