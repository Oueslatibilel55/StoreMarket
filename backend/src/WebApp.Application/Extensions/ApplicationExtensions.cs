using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Application.Interfaces;
using WebApp.Application.Options;
using WebApp.Application.Payments;
using WebApp.Application.Services;

namespace WebApp.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<BootstrapAdminOptions>(config.GetSection(BootstrapAdminOptions.SectionName));

        services.AddScoped<AuthService>();
        services.AddScoped<RoleRequestService>();
        services.AddScoped<AdminUserService>();
        services.AddScoped<AuditService>();
        services.AddScoped<PlatformSettingsService>();
        services.AddScoped<StoreCategoryService>();
        services.AddScoped<ImageService>();
        services.AddScoped<StoreService>();
        services.AddScoped<ModerationService>();
        services.AddScoped<ProductCategoryService>();
        services.AddScoped<ProductService>();
        services.AddScoped<AffiliateCodeGenerator>();
        services.AddScoped<PartnershipService>();
        services.AddScoped<AffiliateCodeService>();
        services.AddScoped<CartService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<OrderLifecycleService>();
        services.AddScoped<CoinLedgerService>();
        services.AddScoped<PayoutService>();
        services.AddSingleton<IPaymentMethod, CashOnDelivery>();
        return services;
    }
}
