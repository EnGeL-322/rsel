using Comercium.Domain;

namespace Comercium.Application;

public sealed record CustomerCard(int Id, string Name, string? Document, string Phone, bool Wholesale, bool Active)
{
    public string Tariff => Wholesale ? "Mayorista" : "Minorista";
}
public sealed record CustomerSummary(CustomerCard Customer, int Purchases, long Spent);

public interface ICustomers
{
    IReadOnlyList<CustomerSummary> Search(Session session, string term);
    IReadOnlyList<CustomerCard> ForSale(Session session);
    int Save(Session session, CustomerCard customer);
    IReadOnlyList<SaleRow> History(Session session, int customerId);
}

public sealed record CashShift(long Id, DateTime OpenedUtc, DateTime? ClosedUtc, long Opening, long Expected, long? Counted)
{
    public long? Difference => Counted - Expected;
}
public sealed record CashMovement(DateTime AtUtc, CashKind Kind, long Amount, string Note, string User);
public sealed record CashView(CashShift? Open, IReadOnlyList<CashShift> Shifts);

public interface ICash
{
    CashView Read(Session session);
    IReadOnlyList<CashMovement> Entries(Session session, long shift);
    bool IsOpen(Session session);
    void Open(Session session, long opening);
    void Move(Session session, long shift, Guid operation, CashKind kind, long amount, string note);
    void Close(Session session, long shift, long counted);
}

public sealed record PlanProfile(string Mission, string Vision, string Values, string Strengths,
    string Opportunities, string Weaknesses, string Threats);
public sealed record Goal(long Id, string Name, string Strategy, string Evaluation, string Indicator, string Unit,
    decimal Target, DateOnly Due, bool Completed);
public sealed record Reading(DateTime AtUtc, decimal Value, string Note, string User);

public interface IStrategy
{
    PlanProfile Profile(Session session);
    void SaveProfile(Session session, PlanProfile profile);
    IReadOnlyList<Goal> Goals(Session session);
    long SaveGoal(Session session, Goal goal);
    IReadOnlyList<Reading> Readings(Session session, long goal);
    void Measure(Session session, long goal, decimal value, string note);
}
