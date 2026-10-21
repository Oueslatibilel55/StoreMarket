using Microsoft.Extensions.Options;
using WebApp.Application.DTOs;
using WebApp.Application.Errors;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Application.Options;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Services;

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    AffiliateCodeGenerator affiliateCodes,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IOptions<BootstrapAdminOptions> bootstrapAdmins,
    TimeProvider clock)
{
    public async Task<Result<AuthSession>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var created = request.AccountType switch
        {
            Role.Buyer => User.Register(request.Email, request.Password, request.FullName, request.Phone, passwordHasher.Hash),
            Role.Affiliate => User.RegisterAffiliate(request.Email, request.Password, request.FullName, request.Phone,
                passwordHasher.Hash, await affiliateCodes.GenerateAsync(ct).ConfigureAwait(false), request.AcceptAffiliateTerms, Now),
            // StoreOwner / Admin are never self-assigned: role request (admin approves) or bootstrap config.
            _ => Result.Fail<User>(DomainErrors.User.AccountTypeInvalid),
        };
        if (!created.IsSuccess) return Result.Fail<AuthSession>(created.Error!);

        var user = created.Value;
        if (await users.EmailExistsAsync(user.Email, ct).ConfigureAwait(false))
            return Result.Fail<AuthSession>(AuthErrors.EmailTaken);

        users.Add(user);
        PromoteBootstrapAdmin(user);
        user.RecordLogin(Now);
        var session = StartSession(user, familyId: null);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(session);
    }

    public async Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = User.NormalizeEmail(request.Email);
        var user = email is null ? null : await users.GetByEmailAsync(email, ct).ConfigureAwait(false);

        // Same error whether the email or the password is wrong: never reveal which accounts exist.
        if (user is null || !passwordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
            return Result.Fail<AuthSession>(AuthErrors.InvalidCredentials);

        if (!user.IsActive) return Result.Fail<AuthSession>(AuthErrors.AccountBlocked);

        PromoteBootstrapAdmin(user);
        user.RecordLogin(Now);
        var session = StartSession(user, familyId: null);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(session);
    }

    public async Task<Result<AuthSession>> RefreshAsync(string? rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawRefreshToken)) return Result.Fail<AuthSession>(AuthErrors.InvalidRefreshToken);

        var token = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(rawRefreshToken), ct).ConfigureAwait(false);
        if (token is null) return Result.Fail<AuthSession>(AuthErrors.InvalidRefreshToken);

        if (token.IsRevoked)
        {
            // A rotated token came back: it was stolen or replayed. Kill the whole session family.
            await RevokeFamilyAsync(token.FamilyId, ct).ConfigureAwait(false);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return Result.Fail<AuthSession>(AuthErrors.InvalidRefreshToken);
        }

        if (!token.IsActive(Now)) return Result.Fail<AuthSession>(AuthErrors.InvalidRefreshToken);

        var user = await users.GetByIdAsync(token.UserId, ct).ConfigureAwait(false);
        if (user is null || !user.IsActive)
        {
            token.Revoke(Now);
            await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
            return Result.Fail<AuthSession>(user is null ? AuthErrors.InvalidRefreshToken : AuthErrors.AccountBlocked);
        }

        var session = StartSession(user, token.FamilyId, out var newTokenId);
        token.Revoke(Now, replacedBy: newTokenId);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        return Result.Ok(session);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawRefreshToken)) return;

        var token = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(rawRefreshToken), ct).ConfigureAwait(false);
        if (token is null) return;

        await RevokeFamilyAsync(token.FamilyId, ct).ConfigureAwait(false);
        await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct).ConfigureAwait(false);
        return user is null ? Result.Fail<UserDto>(AuthErrors.UserNotFound) : Result.Ok(UserDto.From(user));
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private void PromoteBootstrapAdmin(User user)
    {
        if (bootstrapAdmins.Value.Contains(user.Email)) user.GrantRole(Role.Admin);
    }

    private AuthSession StartSession(User user, Guid? familyId) => StartSession(user, familyId, out _);

    private AuthSession StartSession(User user, Guid? familyId, out Guid refreshTokenId)
    {
        var access = tokenService.CreateAccessToken(user);
        var refresh = tokenService.CreateRefreshToken();
        var entity = RefreshToken.Issue(user.Id, refresh.TokenHash, refresh.ExpiresAt, familyId);
        refreshTokens.Add(entity);
        refreshTokenId = entity.Id;

        return new AuthSession(
            new AuthResponse(access.Token, access.ExpiresAt, UserDto.From(user)),
            refresh.RawToken,
            refresh.ExpiresAt);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        foreach (var active in await refreshTokens.GetActiveInFamilyAsync(familyId, ct).ConfigureAwait(false))
            active.Revoke(Now);
    }
}
