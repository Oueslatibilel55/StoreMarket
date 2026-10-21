using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

    public Task<bool> AffiliateCodeExistsAsync(string code, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.AffiliateCode == code, ct);

    public Task<User?> GetByAffiliateCodeAsync(string code, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.AffiliateCode == code, ct);

    public void Add(User user) => db.Users.Add(user);
}
