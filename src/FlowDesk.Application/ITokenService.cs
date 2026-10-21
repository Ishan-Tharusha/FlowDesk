using FlowDesk.Domain;

namespace FlowDesk.Application;

public record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
}