using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public interface IPlatformSettingRepository
{
    Task<PlatformSetting?> GetByKeyAsync(string key, CancellationToken ct = default);
    void Add(PlatformSetting setting);
}
