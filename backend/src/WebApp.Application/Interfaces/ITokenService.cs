using WebApp.Domain.Entities;

namespace WebApp.Application.Interfaces;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public sealed record NewRefreshToken(string RawToken, string TokenHash, DateTime ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
    NewRefreshToken CreateRefreshToken();
    string HashRefreshToken(string rawToken);
}
