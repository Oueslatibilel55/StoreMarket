using WebApp.Application.Interfaces;
using WebApp.Application.Services;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Settings;

namespace WebApp.UnitTests;

public sealed class AffiliateCodeGeneratorTests
{
    /// <summary>Says "taken" for the first <paramref name="taken"/> candidates, and records every check.</summary>
    private sealed class FakeUsers(int taken) : IUserRepository
    {
        public List<string> Checked { get; } = [];

        public Task<bool> AffiliateCodeExistsAsync(string code, CancellationToken ct = default)
        {
            Checked.Add(code);
            return Task.FromResult(Checked.Count <= taken);
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<User?> GetByAffiliateCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public void Add(User user) => throw new NotSupportedException();
    }

    private sealed class FakeSettings(int letters, int minDigits) : IPlatformSettings
    {
        public Task<int> GetIntAsync(string key, CancellationToken ct = default) => Task.FromResult(key switch
        {
            SettingKeys.AffiliateCodeLetters => letters,
            SettingKeys.AffiliateCodeMinDigits => minDigits,
            _ => throw new ArgumentException(key, nameof(key)),
        });

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<decimal> GetDecimalAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public void Invalidate() { }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Generate_UsesTheConfiguredShape_AndChecksTheDatabase()
    {
        var users = new FakeUsers(taken: 0);

        var code = await new AffiliateCodeGenerator(users, new FakeSettings(letters: 4, minDigits: 3)).GenerateAsync(Ct);

        Assert.Equal(code, Assert.Single(users.Checked));
        Assert.Matches("^[A-Z]{4}-[0-9]{3}$", code);
        Assert.True(AffiliateCode.IsValid(code));
    }

    [Fact]
    public async Task Generate_TakenCode_IsNeverReturned_TriesAgain()
    {
        var users = new FakeUsers(taken: 3);

        var code = await new AffiliateCodeGenerator(users, new FakeSettings(3, 2)).GenerateAsync(Ct);

        Assert.Equal(4, users.Checked.Count);
        Assert.Equal(users.Checked[^1], code);
    }

    [Fact]
    public async Task Generate_ManyCollisions_WidensTheNumber()
    {
        // 8 attempts per length: the 9th candidate has one more digit.
        var users = new FakeUsers(taken: 8);

        var code = await new AffiliateCodeGenerator(users, new FakeSettings(3, 2)).GenerateAsync(Ct);

        Assert.Matches("^[A-Z]{3}-[0-9]{3}$", code);
    }
}
