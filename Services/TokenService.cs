using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using kuv_api.Data;
using kuv_api.Models;
using Microsoft.IdentityModel.Tokens;

namespace kuv_api.Services;

public class TokenService
{
    private readonly KuvDbContext dbContext;
    private readonly IConfiguration configuration;

    public TokenService(KuvDbContext dbContext, IConfiguration configuration)
    {
        this.dbContext = dbContext;
        this.configuration = configuration;
    }

    public async Task<TokenResponse> IssueTokens(User user, CancellationToken cancellationToken)
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

    public static string CreateRefreshToken(DateTime expiresAt)
    {
        var randomPart = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"{randomPart}.{expiresAt.Ticks}";
    }

    public static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public static bool HasValidRefreshTokenExpiry(string token)
    {
        var separator = token.LastIndexOf('.');
        return separator > 0 &&
            long.TryParse(token[(separator + 1)..], out var ticks) &&
            new DateTime(ticks, DateTimeKind.Utc) > DateTime.UtcNow;
    }
}

public sealed record TokenResponse(string AccessToken, string RefreshToken, double ExpiresIn);