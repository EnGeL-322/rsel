using Comercium.Application;
using Comercium.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Persistence;

internal static class StoreAccess
{
    public static void Demand(StoreContext db, Session session, Permission allowed)
    {
        var account = db.Users.Where(user => user.Id == session.UserId && user.Active)
            .Select(user => (Permission?)user.Role.Permissions).SingleOrDefault();
        if (account is null || (account.Value & allowed) == 0)
            throw new BusinessException("Tu cuenta no tiene permiso para realizar esta operación.");
    }

    public static void Log(StoreContext db, int userId, DateTime now, string action, string detail) =>
        db.Audit.Add(new AuditEntry { UserId = userId, OccurredUtc = now, Action = action, Detail = detail });

    public static string Required(string value, string label, int limit)
    {
        var text = value.Trim();
        if (text.Length == 0 || text.Length > limit)
            throw new BusinessException($"{label} debe tener entre 1 y {limit} caracteres.");
        return text;
    }

    public static Product FindProduct(StoreContext db, int id) =>
        db.Products.Include(product => product.Inventory).Include(product => product.Category)
            .SingleOrDefault(product => product.Id == id) ?? throw new BusinessException("Selecciona un producto válido.");

    public static void Move(StoreContext db, Product product, int userId, Guid operation, DateTime now,
        MovementKind kind, int change, string reason)
    {
        var balance = (long)product.Inventory.OnHand + change;
        if (balance < 0 || balance > int.MaxValue)
            throw new BusinessException($"La operación excede las existencias permitidas para {product.Name}.");
        product.Inventory.OnHand = (int)balance;
        db.Movements.Add(new InventoryMovement
        {
            ProductId = product.Id, UserId = userId, OperationId = operation, OccurredUtc = now,
            Kind = kind, Change = change, Balance = (int)balance, Reason = reason
        });
    }
}
