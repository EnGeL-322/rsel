using Comercium.Application;
using Comercium.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Persistence;

public sealed class CashRegister(StoreDatabase database) : ICash
{
    public CashView Read(Session session)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Finance);
        var shifts = db.CashSessions.OrderByDescending(shift => shift.Id).Select(shift => new CashShift(shift.Id,
            shift.OpenedUtc, shift.ClosedUtc, shift.Opening, shift.ExpectedAtClose ?? shift.Opening + shift.Entries.Sum(entry => entry.Amount),
            shift.Counted)).ToList();
        return new CashView(shifts.SingleOrDefault(shift => shift.ClosedUtc == null), shifts);
    }

    public IReadOnlyList<CashMovement> Entries(Session session, long shift)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Finance);
        return (from entry in db.CashEntries join user in db.Users on entry.UserId equals user.Id
                where entry.CashSessionId == shift orderby entry.Id descending
                select new CashMovement(entry.AtUtc, entry.Kind, entry.Amount, entry.Note, user.Name)).ToList();
    }

    public bool IsOpen(Session session)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Sell | Permission.Finance);
        return Active(db) != null;
    }

    public void Open(Session session, long opening)
    {
        if (opening is < 0 or > 100_000_000_000)
            throw new BusinessException("Ingresa un saldo inicial válido.");
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Finance);
        if (Active(db) != null)
            throw new BusinessException("Ya hay una caja abierta.");
        var now = DateTime.UtcNow;
        var shift = new CashSession { Opening = opening, OpenedById = session.UserId, OpenedUtc = now };
        db.CashSessions.Add(shift);
        db.SaveChanges();
        StoreAccess.Log(db, session.UserId, now, "Caja abierta", $"Caja {shift.Id}");
        db.SaveChanges();
        transaction.Commit();
    }

    public void Move(Session session, long shift, Guid operation, CashKind kind, long amount, string note)
    {
        if (operation == Guid.Empty || amount is <= 0 or > 100_000_000_000 || kind is not (CashKind.Ingreso or CashKind.Gasto or CashKind.Retiro))
            throw new BusinessException("Revisa el importe y el tipo de movimiento.");
        note = StoreAccess.Required(note, "El concepto", 250);
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Finance);
        if (db.CashEntries.Any(entry => entry.OperationId == operation && entry.CashSessionId == shift))
            return;
        var current = RequireOpen(db, shift);
        var now = DateTime.UtcNow;
        db.CashEntries.Add(new CashEntry { CashSessionId = current.Id, UserId = session.UserId, AtUtc = now, Kind = kind,
            Amount = kind == CashKind.Ingreso ? amount : -amount, Note = note, OperationId = operation });
        StoreAccess.Log(db, session.UserId, now, "Movimiento de caja", $"Caja {shift} · {kind}");
        db.SaveChanges();
        transaction.Commit();
    }

    public void Close(Session session, long shift, long counted)
    {
        if (counted is < 0 or > 100_000_000_000)
            throw new BusinessException("Ingresa el efectivo contado.");
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Finance);
        var current = RequireOpen(db, shift);
        var now = DateTime.UtcNow;
        current.Counted = counted;
        current.ExpectedAtClose = current.Opening + db.CashEntries.Where(entry => entry.CashSessionId == shift).Sum(entry => entry.Amount);
        current.ClosedUtc = now;
        current.ClosedById = session.UserId;
        StoreAccess.Log(db, session.UserId, now, "Caja cerrada", $"Caja {shift} · Diferencia en céntimos: {counted - current.ExpectedAtClose}");
        db.SaveChanges();
        transaction.Commit();
    }

    internal static CashSession? Active(StoreContext db) => db.CashSessions.SingleOrDefault(shift => shift.ClosedUtc == null);

    internal static void Post(StoreContext db, CashSession? shift, Session session, DateTime now, CashKind kind, long amount, long source, string note)
    {
        if (shift == null || amount == 0)
            return;
        db.CashEntries.Add(new CashEntry { CashSessionId = shift.Id, UserId = session.UserId, AtUtc = now,
            Kind = kind, Amount = amount, SourceId = source, Note = note });
    }

    private static CashSession RequireOpen(StoreContext db, long id) => Active(db) is { } shift && shift.Id == id
        ? shift : throw new BusinessException("La caja seleccionada ya no está abierta. Actualiza la pantalla.");
}
