using Comercium.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Comercium.Infrastructure.Persistence;

public sealed class StoreContext(DbContextOptions<StoreContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PriceEntry> Prices => Set<PriceEntry>();
    public DbSet<InventoryMovement> Movements => Set<InventoryMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Dispatch> Dispatches => Set<Dispatch>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchasePayment> PurchasePayments => Set<PurchasePayment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>().HasIndex(user => user.Login).IsUnique();
        model.Entity<Role>().HasIndex(role => role.Name).IsUnique();
        model.Entity<AuditEntry>().HasIndex(entry => entry.OccurredUtc);
        model.Entity<AuditEntry>().HasOne<User>().WithMany().HasForeignKey(entry => entry.UserId);

        model.Entity<Category>().HasIndex(category => category.NormalizedName).IsUnique();
        model.Entity<Product>().HasIndex(product => product.Code).IsUnique();
        model.Entity<Product>().HasIndex(product => product.Name);
        model.Entity<Product>().ToTable(table => table.HasCheckConstraint("CK_Product_Prices", "Cost >= 0 AND Retail > 0 AND Wholesale > 0"));
        model.Entity<Product>().HasOne(product => product.Inventory).WithOne().HasForeignKey<Inventory>(inventory => inventory.ProductId);
        model.Entity<Inventory>().HasKey(inventory => inventory.ProductId);
        model.Entity<Inventory>().ToTable("Inventory", table => table.HasCheckConstraint("CK_Inventory_Nonnegative", "OnHand >= 0 AND Minimum >= 0"));
        model.Entity<PriceEntry>().HasOne<Product>().WithMany().HasForeignKey(entry => entry.ProductId);
        model.Entity<PriceEntry>().HasOne<User>().WithMany().HasForeignKey(entry => entry.UserId);
        model.Entity<InventoryMovement>().HasOne<Inventory>().WithMany().HasForeignKey(entry => entry.ProductId);
        model.Entity<InventoryMovement>().HasOne<User>().WithMany().HasForeignKey(entry => entry.UserId);
        model.Entity<InventoryMovement>().HasIndex(entry => new { entry.OperationId, entry.ProductId }).IsUnique();
        model.Entity<InventoryMovement>().HasIndex(entry => new { entry.ProductId, entry.OccurredUtc });
        model.Entity<InventoryMovement>().ToTable(table => table.HasCheckConstraint("CK_Movements_Change", "Change <> 0 AND Balance >= 0"));

        model.Entity<Sale>().HasIndex(sale => sale.OperationId).IsUnique();
        model.Entity<Sale>().HasIndex(sale => sale.OccurredUtc);
        model.Entity<Sale>().HasOne<User>().WithMany().HasForeignKey(sale => sale.UserId);
        model.Entity<Sale>().HasOne<User>().WithMany().HasForeignKey(sale => sale.VoidedById);
        model.Entity<Sale>().ToTable(table => table.HasCheckConstraint("CK_Sales_Total", "Subtotal > 0 AND Discount >= 0 AND Total = Subtotal - Discount AND Total >= 0 AND Cost >= 0"));
        model.Entity<SaleLine>().HasOne<Product>().WithMany().HasForeignKey(line => line.ProductId);
        model.Entity<SaleLine>().ToTable("SaleLines", table => table.HasCheckConstraint("CK_SaleLines_Quantity", "Quantity > 0 AND UnitPrice > 0 AND UnitCost >= 0"));
        model.Entity<Sale>().HasOne(sale => sale.Payment).WithOne().HasForeignKey<Payment>(payment => payment.SaleId);
        model.Entity<Payment>().HasKey(payment => payment.SaleId);
        model.Entity<Payment>().ToTable("Payments", table => table.HasCheckConstraint("CK_Payments_Amount", "Amount >= 0 AND Received >= Amount"));
        model.Entity<Sale>().HasOne(sale => sale.Dispatch).WithOne().HasForeignKey<Dispatch>(dispatch => dispatch.SaleId);
        model.Entity<Dispatch>().HasKey(dispatch => dispatch.SaleId);
        model.Entity<Dispatch>().HasIndex(dispatch => dispatch.Status);
        model.Entity<Dispatch>().HasOne<User>().WithMany().HasForeignKey(dispatch => dispatch.StartedById);
        model.Entity<Dispatch>().HasOne<User>().WithMany().HasForeignKey(dispatch => dispatch.ReadyById);
        model.Entity<Dispatch>().HasOne<User>().WithMany().HasForeignKey(dispatch => dispatch.DeliveredById);
        model.Entity<Dispatch>().HasMany(dispatch => dispatch.Lines).WithOne().HasForeignKey(line => line.DispatchId);
        model.Entity<DispatchLine>().HasKey(line => line.SaleLineId);
        model.Entity<DispatchLine>().HasOne(line => line.SaleLine).WithOne().HasForeignKey<DispatchLine>(line => line.SaleLineId);
        model.Entity<DispatchLine>().HasOne<User>().WithMany().HasForeignKey(line => line.VerifiedById);

        model.Entity<Supplier>().HasIndex(supplier => supplier.NormalizedName).IsUnique();
        model.Entity<Purchase>().HasIndex(purchase => purchase.OperationId).IsUnique();
        model.Entity<Purchase>().HasIndex(purchase => purchase.AtUtc);
        model.Entity<Purchase>().HasOne<User>().WithMany().HasForeignKey(purchase => purchase.UserId);
        model.Entity<Purchase>().ToTable(table => table.HasCheckConstraint("CK_Purchases_Total", "Total > 0"));
        model.Entity<PurchaseLine>().HasIndex(line => new { line.PurchaseId, line.ProductId }).IsUnique();
        model.Entity<PurchaseLine>().ToTable("PurchaseLines", table => table.HasCheckConstraint("CK_PurchaseLines_Value", "Quantity > 0 AND UnitCost > 0"));
        model.Entity<PurchasePayment>().HasIndex(payment => payment.OperationId).IsUnique();
        model.Entity<PurchasePayment>().HasOne<User>().WithMany().HasForeignKey(payment => payment.UserId);
        model.Entity<PurchasePayment>().ToTable(table => table.HasCheckConstraint("CK_PurchasePayments_Amount", "Amount > 0"));

        foreach (var foreignKey in model.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()))
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
    }
}

public sealed class StoreDatabase(string path, bool pooling = true)
{
    private readonly DbContextOptions<StoreContext> options = new DbContextOptionsBuilder<StoreContext>()
        .UseSqlite(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, Pooling = pooling, DefaultTimeout = 5 }.ToString())
        .Options;

    public StoreContext Open() => new(options);

    public void Migrate()
    {
        using var db = Open();
        db.Database.Migrate();
    }
}

public sealed class StoreDesignFactory : IDesignTimeDbContextFactory<StoreContext>
{
    public StoreContext CreateDbContext(string[] args) => new StoreDatabase("comercium-design.db").Open();
}
