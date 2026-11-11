using Comercium.Application;
using Comercium.Domain;
using Comercium.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Security;

public sealed class Accounts(StoreDatabase database) : IAccounts
{
    private const string Dummy = "pbkdf2-sha256$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public bool NeedsSetup()
    {
        using var db = database.Open();
        return !db.Users.Any();
    }

    public Session CreateOwner(string name, string login, string password)
    {
        name = StoreAccess.Required(name, "El nombre", 100);
        login = StoreAccess.Required(login, "El usuario", 60).ToUpperInvariant();
        ValidatePassword(password);
        var hash = PasswordHash.Create(password);

        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        if (db.Users.Any())
            throw new BusinessException("La cuenta propietaria ya está registrada.");

        var user = new User
        {
            Name = name, Login = login, PasswordHash = hash,
            Role = new Role { Name = "Propietaria", Permissions = Enum.GetValues<Permission>().Aggregate((a, b) => a | b) }
        };
        db.Users.Add(user);
        db.SaveChanges();
        StoreAccess.Log(db, user.Id, DateTime.UtcNow, "Cuenta creada", "Registro inicial de la cuenta propietaria");
        db.SaveChanges();
        transaction.Commit();
        return ToSession(user);
    }

    public Session SignIn(string login, string password)
    {
        if (login.Length > 60 || password.Length > 128)
            throw new BusinessException("El usuario o la contraseña son incorrectos.");

        var key = login.Trim().ToUpperInvariant();
        using var db = database.Open();
        var user = db.Users.Include(user => user.Role).SingleOrDefault(user => user.Login == key && user.Active);
        if (!PasswordHash.Verify(password, user?.PasswordHash ?? Dummy) || user is null)
            throw new BusinessException("El usuario o la contraseña son incorrectos.");
        return ToSession(user);
    }

    public void ChangePassword(Session session, string current, string replacement)
    {
        ValidatePassword(replacement);
        using var db = database.Open();
        var user = db.Users.SingleOrDefault(user => user.Id == session.UserId && user.Active);
        if (user is null || current.Length > 128 || !PasswordHash.Verify(current, user.PasswordHash))
            throw new BusinessException("La contraseña actual es incorrecta.");

        user.PasswordHash = PasswordHash.Create(replacement);
        StoreAccess.Log(db, user.Id, DateTime.UtcNow, "Contraseña cambiada", "Cambio realizado desde Cuenta");
        db.SaveChanges();
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length is < 12 or > 128 || string.IsNullOrWhiteSpace(password))
            throw new BusinessException("Usa una contraseña de entre 12 y 128 caracteres.");
    }

    private static Session ToSession(User user) => new(user.Id, user.Name, user.Role.Permissions);
}
