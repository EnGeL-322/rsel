namespace Comercium.Domain;

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
}

public sealed class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Unit { get; set; } = "";
    public string Location { get; set; } = "";
    public long Cost { get; set; }
    public long Retail { get; set; }
    public long Wholesale { get; set; }
    public byte[]? Photo { get; set; }
    public bool Active { get; set; } = true;
    public Inventory Inventory { get; set; } = new();
}

public sealed class Inventory
{
    public int ProductId { get; set; }
    public int OnHand { get; set; }
    public int Minimum { get; set; }
}

public sealed class PriceEntry
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public DateTime OccurredUtc { get; set; }
    public long Cost { get; set; }
    public long Retail { get; set; }
    public long Wholesale { get; set; }
}

public enum MovementKind { Entrada, Salida, Ajuste, Venta, Anulación, Compra }

public sealed class InventoryMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public Guid OperationId { get; set; }
    public DateTime OccurredUtc { get; set; }
    public MovementKind Kind { get; set; }
    public int Change { get; set; }
    public int Balance { get; set; }
    public string Reason { get; set; } = "";
}
