using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WebApp.Domain.Entities;

namespace WebApp.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<RoleRequest> RoleRequests => Set<RoleRequest>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<StoreCategory> StoreCategories => Set<StoreCategory>();
    public DbSet<StoredImage> Images => Set<StoredImage>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Partnership> Partnerships => Set<Partnership>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<CoinEntry> CoinEntries => Set<CoinEntry>();
    public DbSet<CoinAccount> CoinAccounts => Set<CoinAccount>();
    public DbSet<PayoutRequest> PayoutRequests => Set<PayoutRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.HasSequence<long>(Configurations.OrderConfiguration.NumberSequence);
        modelBuilder.HasSequence<long>(Configurations.PayoutRequestConfiguration.NumberSequence);

        // Guid keys are assigned by the domain (BaseEntity), never by the database.
        // With EF's default (ValueGeneratedOnAdd) a new child reached through a navigation
        // has a non-empty key, so EF tracks it as Modified instead of Added and SaveChanges
        // issues an UPDATE that matches no row (DbUpdateConcurrencyException).
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetProperties())
                     .Where(p => p.IsPrimaryKey() && p.ClrType == typeof(Guid)))
        {
            property.ValueGenerated = ValueGenerated.Never;
        }

        base.OnModelCreating(modelBuilder);
    }
}
