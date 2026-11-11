namespace Comercium.Domain;

[Flags]
public enum Permission
{
    ViewDashboard = 1,
    ManageCatalog = 2,
    AdjustInventory = 4,
    Sell = 8,
    ApplyDiscount = 16,
    Dispatch = 32,
    VoidSale = 64,
    ManageBackups = 128,
    Buy = 256
}

public sealed class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public Permission Permissions { get; set; }
}

public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public bool Active { get; set; } = true;
}

public sealed class AuditEntry
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public DateTime OccurredUtc { get; set; }
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
}
