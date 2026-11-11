namespace Comercium.Domain;

public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string SearchName { get; set; } = "";
    public string? Document { get; set; }
    public string Phone { get; set; } = "";
    public bool Wholesale { get; set; }
    public bool Active { get; set; } = true;
}

public enum CashKind { Ingreso, Gasto, Retiro, Venta, Reversión, Proveedor }

public sealed class CashSession
{
    public long Id { get; set; }
    public int OpenedById { get; set; }
    public DateTime OpenedUtc { get; set; }
    public long Opening { get; set; }
    public int? ClosedById { get; set; }
    public DateTime? ClosedUtc { get; set; }
    public long? Counted { get; set; }
    public long? ExpectedAtClose { get; set; }
    public List<CashEntry> Entries { get; set; } = [];
}

public sealed class CashEntry
{
    public long Id { get; set; }
    public long CashSessionId { get; set; }
    public int UserId { get; set; }
    public DateTime AtUtc { get; set; }
    public CashKind Kind { get; set; }
    public long Amount { get; set; }
    public string Note { get; set; } = "";
    public long? SourceId { get; set; }
    public Guid? OperationId { get; set; }
}

public sealed class StrategyPlan
{
    public int Id { get; set; }
    public string Mission { get; set; } = "";
    public string Vision { get; set; } = "";
    public string Values { get; set; } = "";
    public string Strengths { get; set; } = "";
    public string Opportunities { get; set; } = "";
    public string Weaknesses { get; set; } = "";
    public string Threats { get; set; } = "";
}

public sealed class Objective
{
    public long Id { get; set; }
    public int StrategyPlanId { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Strategy { get; set; } = "";
    public string Evaluation { get; set; } = "";
    public string Indicator { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Target { get; set; }
    public DateOnly Due { get; set; }
    public bool Completed { get; set; }
    public List<IndicatorReading> Readings { get; set; } = [];
}

public sealed class IndicatorReading
{
    public long Id { get; set; }
    public long ObjectiveId { get; set; }
    public int UserId { get; set; }
    public DateTime AtUtc { get; set; }
    public decimal Value { get; set; }
    public string Note { get; set; } = "";
}
