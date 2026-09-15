using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using kuv_api.Data;
using kuv_api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace kuv_api.Controllers;

[ApiController]
[Route("[controller]")]
public class SessionController : ControllerBase
{
    private readonly ApplicationDbContext dbContext;
    private readonly IConfiguration configuration;
    private readonly IPasswordHasher<User> passwordHasher;

    public SessionController(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        IPasswordHasher<User> passwordHasher)
    {
        this.dbContext = dbContext;
        this.configuration = configuration;
        this.passwordHasher = passwordHasher;
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Username == request.Username,
            cancellationToken);

        if (user is null || passwordHasher.VerifyHashedPassword(user, user.Hashword, request.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        return Ok(await IssueTokens(user, cancellationToken));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var refreshTokenHash = HashToken(request.RefreshToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.RefreshToken == refreshTokenHash,
            cancellationToken);

        if (user is null || !HasValidRefreshTokenExpiry(request.RefreshToken))
        {
            return Unauthorized();
        }

        return Ok(await IssueTokens(user, cancellationToken));
    }

    private async Task<TokenResponse> IssueTokens(User user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var accessTokenLifetime = TimeSpan.FromMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 15));
        var refreshToken = CreateRefreshToken(now.AddDays(7));
        user.RefreshToken = HashToken(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            notBefore: now,
            expires: now.Add(accessTokenLifetime),
            signingCredentials: credentials);

        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            refreshToken,
            accessTokenLifetime.TotalSeconds);
    }

    private static string CreateRefreshToken(DateTime expiresAt)
    {
        var randomPart = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        return $"{randomPart}.{expiresAt.Ticks}";
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static bool HasValidRefreshTokenExpiry(string token)
    {
        var separator = token.LastIndexOf('.');
        return separator > 0 &&
            long.TryParse(token[(separator + 1)..], out var ticks) &&
            new DateTime(ticks, DateTimeKind.Utc) > DateTime.UtcNow;
    }

    public sealed record LoginRequest(string Username, string Password);
    public sealed record RefreshRequest(string RefreshToken);
    public sealed record TokenResponse(string AccessToken, string RefreshToken, double ExpiresIn);
}