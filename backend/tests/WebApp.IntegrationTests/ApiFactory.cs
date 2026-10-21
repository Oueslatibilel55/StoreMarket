using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WebApp.Infrastructure.Persistence;

[assembly: AssemblyFixture(typeof(WebApp.IntegrationTests.ApiFactory))]

namespace WebApp.IntegrationTests;

/// <summary>
/// Boots the API in-process against a throw-away PostgreSQL 17 container (Docker required),
/// with every migration applied. Shared by all integration tests of the assembly.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Throw-away upload folder, deleted with the factory.</summary>
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), $"webapp-tests-{Guid.NewGuid():N}");

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
        builder.UseSetting("Jwt:Secret", "integration-tests-secret-at-least-32-characters-long");
        builder.UseSetting("RateLimiting:AuthPerMinute", "10000");
        builder.UseSetting("RateLimiting:AffiliateCodePerMinute", "10000");
        builder.UseSetting("RateLimiting:CartPerMinute", "10000");
        builder.UseSetting("BootstrapAdmin:Emails:0", TestApi.AdminEmail);
        builder.UseSetting("Storage:RootPath", StorageRoot);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
        if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, recursive: true);
    }
}
