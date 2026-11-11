using Comercium.Application;
using Comercium.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Persistence;

public sealed class Catalog(StoreDatabase database) : ICatalog
{
    private const long MaxPrice = 100_000_000;

    public IReadOnlyList<ProductRow> Search(Session session, string text, string? category = null, bool activeOnly = false)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.ManageCatalog | Permission.AdjustInventory | Permission.Sell | Permission.Buy);
        var query = db.Products.AsNoTracking();
        var term = text.Trim().ToUpperInvariant();
        if (term.Length > 0)
            query = query.Where(product => product.Code.Contains(term) || StoreContext.Upper(product.Name).Contains(term));
        if (!string.IsNullOrWhiteSpace(category))
        {
            var key = category.Trim().ToUpperInvariant();
            query = query.Where(product => product.Category.NormalizedName == key);
        }
        if (activeOnly)
            query = query.Where(product => product.Active);
        return query.OrderBy(product => product.Name).ThenBy(product => product.Code)
            .Select(product => new ProductRow(product.Id, product.Code, product.Name, product.Category.Name,
                product.Unit, product.Location, product.Retail, product.Wholesale, product.Inventory.OnHand,
                product.Inventory.Minimum, product.Active)).ToList();
    }

    public ProductDraft Load(Session session, int id)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.ManageCatalog);
        var product = StoreAccess.FindProduct(db, id);
        return new ProductDraft(product.Id, product.Code, product.Name, product.Description, product.Category.Name,
            product.Unit, product.Location, product.Cost, product.Retail, product.Wholesale, product.Inventory.Minimum,
            product.Photo, product.Active);
    }

    public int Save(Session session, ProductDraft draft)
    {
        var code = StoreAccess.Required(draft.Code, "El código", 48).ToUpperInvariant();
        var name = StoreAccess.Required(draft.Name, "El nombre", 140);
        var categoryName = StoreAccess.Required(draft.Category, "La categoría", 80);
        var unit = StoreAccess.Required(draft.Unit, "La unidad", 40);
        if (draft.Location.Length > 120 || draft.Description.Length > 1000 || draft.Photo?.Length > 4_000_000)
            throw new BusinessException("Revisa la ubicación, la descripción y el tamaño de la foto (máximo 4 MB).");
        if (draft.Cost is < 0 or > MaxPrice || draft.Retail is <= 0 or > MaxPrice || draft.Wholesale is <= 0 or > MaxPrice)
            throw new BusinessException("Ingresa precios válidos. Los precios de venta deben ser mayores a cero.");
        if (draft.Minimum < 0)
            throw new BusinessException("El stock mínimo debe ser cero o mayor.");

        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.ManageCatalog);
        if (db.Products.Any(product => product.Id != draft.Id && product.Code == code))
            throw new BusinessException("Ya existe un producto con ese código.");

        var key = categoryName.ToUpperInvariant();
        var category = db.Categories.SingleOrDefault(category => category.NormalizedName == key)
            ?? new Category { Name = categoryName, NormalizedName = key };
        var product = draft.Id == 0 ? new Product() : StoreAccess.FindProduct(db, draft.Id);
        var priceChanged = draft.Id == 0 || product.Cost != draft.Cost || product.Retail != draft.Retail || product.Wholesale != draft.Wholesale;
        product.Code = code;
        product.Name = name;
        product.Description = draft.Description.Trim();
        product.Category = category;
        product.Unit = unit;
        product.Location = draft.Location.Trim();
        product.Cost = draft.Cost;
        product.Retail = draft.Retail;
        product.Wholesale = draft.Wholesale;
        product.Inventory.Minimum = draft.Minimum;
        product.Photo = draft.Photo;
        product.Active = draft.Active;
        if (draft.Id == 0)
            db.Products.Add(product);
        db.SaveChanges();

        var now = DateTime.UtcNow;
        if (priceChanged)
            db.Prices.Add(new PriceEntry { ProductId = product.Id, UserId = session.UserId, OccurredUtc = now,
                Cost = product.Cost, Retail = product.Retail, Wholesale = product.Wholesale });
        StoreAccess.Log(db, session.UserId, now, draft.Id == 0 ? "Producto creado" : "Producto editado",
            $"{product.Id} · {code} · {(draft.Active ? "Activo" : "Desactivado")}");
        db.SaveChanges();
        transaction.Commit();
        return product.Id;
    }

    public void Move(Session session, int productId, Guid operationId, MovementKind kind, int quantity, string reason)
    {
        if (operationId == Guid.Empty || quantity < 0 || (kind != MovementKind.Ajuste && quantity == 0))
            throw new BusinessException("Ingresa una cantidad válida para el movimiento.");
        reason = StoreAccess.Required(reason, "El motivo", 250);
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.AdjustInventory);
        if (db.Movements.Any(row => row.OperationId == operationId && row.ProductId == productId))
            return;

        var product = StoreAccess.FindProduct(db, productId);
        var change = kind switch
        {
            MovementKind.Entrada => quantity,
            MovementKind.Salida => -quantity,
            MovementKind.Ajuste => quantity - product.Inventory.OnHand,
            _ => throw new BusinessException("Selecciona un tipo de movimiento válido.")
        };
        if (change == 0)
            throw new BusinessException("El conteo coincide con el stock actual.");
        var now = DateTime.UtcNow;
        StoreAccess.Move(db, product, session.UserId, operationId, now, kind, change, reason);
        StoreAccess.Log(db, session.UserId, now, "Inventario actualizado", $"{product.Code} · {kind} · {change:+0;-0} · {reason}");
        db.SaveChanges();
        transaction.Commit();
    }

    public IReadOnlyList<MovementRow> Movements(Session session, int productId)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.AdjustInventory | Permission.ManageCatalog);
        return (from row in db.Movements
                join user in db.Users on row.UserId equals user.Id
                where row.ProductId == productId
                orderby row.Id descending
                select new MovementRow(row.OccurredUtc, row.Kind, row.Change, row.Balance, row.Reason, user.Name)).ToList();
    }

    public IReadOnlyList<PriceRow> Prices(Session session, int productId)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.ManageCatalog);
        return db.Prices.Where(row => row.ProductId == productId).OrderByDescending(row => row.Id)
            .Select(row => new PriceRow(row.OccurredUtc, row.Cost, row.Retail, row.Wholesale)).ToList();
    }
}
