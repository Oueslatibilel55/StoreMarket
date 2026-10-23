using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Application.Interfaces;
using WebApp.Infrastructure.Auditing;
using WebApp.Infrastructure.Events;
using WebApp.Infrastructure.Persistence;
using WebApp.Infrastructure.Queries;
using WebApp.Infrastructure.Repositories;
using WebApp.Infrastructure.Security;
using WebApp.Infrastructure.Settings;
using WebApp.Infrastructure.Storage;

namespace WebApp.Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Under Aspire, `WithReference(webappDb)` injects ConnectionStrings__webapp —
        // named after the database resource, not "DefaultConnection". Prefer it so the
        // orchestrated run uses the database Aspire started, and fall back to
        // DefaultConnection for docker-compose, tests and production.
        var connectionString =
            config.GetConnectionString("webapp")
            ?? config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "No database connection string found. Run through the AppHost, or set it with " +
                "'dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" <value> " +
                "--project src/WebApp.Api' in development, or the " +
                "ConnectionStrings__DefaultConnection environment variable in production.");

        services.AddDbContext<AppDbContext>(opt => opt
            .UseNpgsql(connectionString,
                npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRoleRequestRepository, RoleRequestRepository>();
        services.AddScoped<IPlatformSettingRepository, PlatformSettingRepository>();
        services.AddScoped<IPlatformSettings, PlatformSettingsStore>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IAdminQueries, AdminQueries>();
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<IStoreCategoryRepository, StoreCategoryRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IProductCategoryRepository, ProductCategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IPartnershipRepository, PartnershipRepository>();
        services.AddScoped<IPartnershipQueries, PartnershipQueries>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderQueries, OrderQueries>();
        services.AddScoped<ICoinLedger, CoinLedger>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<IBillingRepository, BillingRepository>();
        services.AddScoped<IBillingQueries, BillingQueries>();
        services.AddScoped<IPayoutQueries, PayoutQueries>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();
        services.AddScoped<ICoinQueries, CoinQueries>();
        services.AddScoped<IStatsQueries, StatsQueries>();
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.SectionName));
        // Disk locally; the database where the disk does not survive a restart (Render free tier, ADR 0006).
        if (config.GetSection(StorageOptions.SectionName).Get<StorageOptions>()?.UseDatabase == true)
        {
            services.AddScoped<DatabaseFileStorage>();
            services.AddScoped<IFileStorage>(sp => sp.GetRequiredService<DatabaseFileStorage>());
        }
        else
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }

        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        services.AddMemoryCache();

        return services;
    }
}
