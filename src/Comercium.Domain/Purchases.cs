namespace Comercium.Domain;

public sealed class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string Phone { get; set; } = "";
}

public sealed class Purchase
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int UserId { get; set; }
    public DateTime AtUtc { get; set; }
    public string Reference { get; set; } = "";
    public long Total { get; set; }
    public List<PurchaseLine> Lines { get; set; } = [];
    public List<PurchasePayment> Payments { get; set; } = [];
}

public sealed class PurchaseLine
{
    public long Id { get; set; }
    public long PurchaseId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public long UnitCost { get; set; }
}

public sealed class PurchasePayment
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public long PurchaseId { get; set; }
    public int UserId { get; set; }
    public DateTime AtUtc { get; set; }
    public long Amount { get; set; }
    public PaymentMethod Method { get; set; }
}
