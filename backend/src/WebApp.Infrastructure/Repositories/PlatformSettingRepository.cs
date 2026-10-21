using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class PlatformSettingRepository(AppDbContext db) : IPlatformSettingRepository
{
    public Task<PlatformSetting?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

    public void Add(PlatformSetting setting) => db.PlatformSettings.Add(setting);
}
