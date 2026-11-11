namespace Comercium.Domain;

public enum PaymentMethod { Efectivo, Tarjeta, Transferencia }
public enum DispatchStatus { Pendiente, EnPreparación, ListoParaRecoger, Entregado, Cancelado }

public sealed class Sale
{
    public long Id { get; set; }
    public Guid OperationId { get; set; }
    public int UserId { get; set; }
    public DateTime OccurredUtc { get; set; }
    public long Subtotal { get; set; }
    public long Discount { get; set; }
    public long Total { get; set; }
    public long Cost { get; set; }
    public DateTime? VoidedUtc { get; set; }
    public int? VoidedById { get; set; }
    public string? VoidReason { get; set; }
    public List<SaleLine> Lines { get; set; } = [];
    public Payment Payment { get; set; } = null!;
    public Dispatch Dispatch { get; set; } = null!;
}

public sealed class SaleLine
{
    public long Id { get; set; }
    public long SaleId { get; set; }
    public int ProductId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public int Quantity { get; set; }
    public long UnitPrice { get; set; }
    public long UnitCost { get; set; }
}

public sealed class Payment
{
    public long SaleId { get; set; }
    public PaymentMethod Method { get; set; }
    public long Amount { get; set; }
    public long Received { get; set; }
    public DateTime? ReversedUtc { get; set; }
}

public sealed class Dispatch
{
    public long SaleId { get; set; }
    public DispatchStatus Status { get; set; }
    public DateTime? StartedUtc { get; set; }
    public int? StartedById { get; set; }
    public DateTime? ReadyUtc { get; set; }
    public int? ReadyById { get; set; }
    public DateTime? DeliveredUtc { get; set; }
    public int? DeliveredById { get; set; }
    public List<DispatchLine> Lines { get; set; } = [];

    public void Advance(int userId, DateTime utcNow)
    {
        switch (Status)
        {
            case DispatchStatus.Pendiente:
                Status = DispatchStatus.EnPreparación;
                StartedUtc = utcNow;
                StartedById = userId;
                break;
            case DispatchStatus.EnPreparación when IsVerified:
                Status = DispatchStatus.ListoParaRecoger;
                ReadyUtc = utcNow;
                ReadyById = userId;
                break;
            case DispatchStatus.ListoParaRecoger when IsVerified:
                Status = DispatchStatus.Entregado;
                DeliveredUtc = utcNow;
                DeliveredById = userId;
                break;
            default:
                throw new BusinessException("Para continuar, el pedido debe estar activo y todos los productos deben estar verificados.");
        }
    }

    private bool IsVerified => Lines.Count > 0 && Lines.All(line => line.VerifiedUtc.HasValue);
}

public sealed class DispatchLine
{
    public long SaleLineId { get; set; }
    public long DispatchId { get; set; }
    public SaleLine SaleLine { get; set; } = null!;
    public DateTime? VerifiedUtc { get; set; }
    public int? VerifiedById { get; set; }
}
