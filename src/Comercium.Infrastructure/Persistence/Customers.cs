using Comercium.Application;
using Comercium.Domain;

namespace Comercium.Infrastructure.Persistence;

public sealed class Customers(StoreDatabase database) : ICustomers
{
    public IReadOnlyList<CustomerSummary> Search(Session session, string term)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Clients);
        var key = term.Trim().ToUpperInvariant();
        return db.Customers.Where(c => c.SearchName.Contains(key) || (c.Document != null && c.Document.Contains(key)) || c.Phone.Contains(key))
            .OrderBy(c => c.Name).Select(c => new CustomerSummary(new CustomerCard(c.Id, c.Name, c.Document, c.Phone, c.Wholesale, c.Active),
                db.Sales.Count(s => s.CustomerId == c.Id && s.VoidedUtc == null),
                db.Sales.Where(s => s.CustomerId == c.Id && s.VoidedUtc == null).Sum(s => s.Total))).ToList();
    }

    public IReadOnlyList<CustomerCard> ForSale(Session session)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Sell);
        return db.Customers.Where(c => c.Active).OrderBy(c => c.Name)
            .Select(c => new CustomerCard(c.Id, c.Name, c.Document, c.Phone, c.Wholesale, c.Active)).ToList();
    }

    public int Save(Session session, CustomerCard card)
    {
        var name = StoreAccess.Required(card.Name, "El nombre", 140);
        var document = string.IsNullOrWhiteSpace(card.Document) ? null : card.Document.Trim().ToUpperInvariant();
        if (document?.Length > 30 || card.Phone.Trim().Length > 40)
            throw new BusinessException("Revisa el documento y el teléfono.");
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Clients);
        if (document != null && db.Customers.Any(c => c.Id != card.Id && c.Document == document))
            throw new BusinessException("Ya existe un cliente con ese documento.");
        var customer = card.Id == 0 ? new Customer() : db.Customers.Find(card.Id) ?? throw new BusinessException("El cliente no existe.");
        customer.Name = name;
        customer.SearchName = name.ToUpperInvariant();
        customer.Document = document;
        customer.Phone = card.Phone.Trim();
        customer.Wholesale = card.Wholesale;
        customer.Active = card.Active;
        if (card.Id == 0)
            db.Customers.Add(customer);
        db.SaveChanges();
        StoreAccess.Log(db, session.UserId, DateTime.UtcNow, "Cliente guardado", $"Cliente {customer.Id}");
        db.SaveChanges();
        transaction.Commit();
        return customer.Id;
    }

    public IReadOnlyList<SaleRow> History(Session session, int customerId)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Clients);
        return db.Sales.Where(s => s.CustomerId == customerId).OrderByDescending(s => s.Id)
            .Select(SalesRegister.Summary).ToList();
    }
}
