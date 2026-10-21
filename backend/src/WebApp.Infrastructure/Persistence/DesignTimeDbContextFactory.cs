using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebApp.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` so migrations don't have to boot the whole API (JWT secret,
/// Aspire wiring...). Targets the Aspire database by default; override with WEBAPP_DB.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnection =
        "Host=localhost;Port=62750;Database=webapp;Username=webapp;Password=webapp_dev";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("WEBAPP_DB") ?? DefaultConnection;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
