using FlowDesk.Application;
using FlowDesk.Domain;
using FlowDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace FlowDesk.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("auth")]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    ITokenService tokens,
    IPasswordHasher hasher,
    IOptions<JwtOptions> jwt) : ControllerBase
{
    private static string? _dummyHash;
    public record LoginRequest(string Email, string Password);
    public record RefreshRequest(string RefreshToken);
    public record AuthResponse(
        string AccessToken, DateTime AccessTokenExpiresAtUtc,
        string RefreshToken, DateTime RefreshTokenExpiresAtUtc);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);   // tenant filter auto

        if (user is null)
        {
            // user nathnath BCrypt time eka samanai wenna (email enumeration walakwanna)
            hasher.Verify(req.Password, _dummyHash ??= hasher.Hash("not-a-real-password"));
            return Invalid("Invalid email or password");
        }

        if (!user.IsActive || !hasher.Verify(req.Password, user.PasswordHash))
            return Invalid("Invalid email or password");

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req)
    {
        var hash = tokens.HashToken(req.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is null)
            return Invalid("Invalid refresh token");

        // Revoke wela thiyena token ekak aye use kala => leak wenna puluwan. User ge okkoma tokens revoke.
        if (stored.RevokedAtUtc is not null)
        {
            var active = await db.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedAtUtc == null)
                .ToListAsync();
            foreach (var t in active) t.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Invalid("Refresh token reuse detected. Aye login wenna.");
        }

        if (stored.ExpiresAtUtc <= DateTime.UtcNow)
            return Invalid("Refresh token expired");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == stored.UserId);
        if (user is null || !user.IsActive)
            return Invalid("User not available");

        stored.RevokedAtUtc = DateTime.UtcNow;            // rotation: palaya eka revoke
        return Ok(await IssueTokensAsync(user));          // aluth pair eka + save (deka ekapara)
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        var hash = tokens.HashToken(req.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored is { RevokedAtUtc: null })
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var access = tokens.CreateAccessToken(user);
        var rawRefresh = tokens.GenerateRefreshToken();
        var refreshExpires = DateTime.UtcNow.AddDays(jwt.Value.RefreshTokenDays);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashToken(rawRefresh),
            ExpiresAtUtc = refreshExpires
        });
        await db.SaveChangesAsync();

        return new AuthResponse(access.Token, access.ExpiresAtUtc, rawRefresh, refreshExpires);
    }

    private ObjectResult Invalid(string title) =>
        Problem(title: title, statusCode: StatusCodes.Status401Unauthorized);
}