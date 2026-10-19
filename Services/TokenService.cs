using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using kuv_api.Data;
using kuv_api.Models;
using Microsoft.IdentityModel.Tokens;

namespace kuv_api.Services;

/// <summary>
/// Issues and validates access and refresh tokens.
/// </summary>
public class TokenService(
    KuvDbContext dbContext, 
    IConfiguration configuration
)
{
    private readonly KuvDbContext dbContext = dbContext;

    private readonly IConfiguration configuration = configuration;

    /// <summary>
    /// Generates a new access/refresh token pair and saves the refresh token hash on the user.
    /// </summary>
    /// <param name="user">
    /// User to issue the tokens for.
    /// </param>
    /// <returns>
    /// The new access token, refresh token and access token lifetime.
    /// </returns>
    public async Task<TokenResponse> IssueTokens(User user)
    {
        var now = DateTime.UtcNow;

        // Access token
        var accessTokenLifetime = TimeSpan.FromMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 15));
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
            signingCredentials: credentials
        );
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // Refresh token
        var refreshTokenLifetime = TimeSpan.FromDays(configuration.GetValue("Jwt:RefreshTokenDays", 7));
        var refreshToken = CreateRefreshToken(now.Add(refreshTokenLifetime));
        user.RefreshToken = HashToken(refreshToken);
        await dbContext.SaveChangesAsync();

        return new TokenResponse(accessToken, refreshToken, accessTokenLifetime.TotalSeconds);
    }

    /// <summary>
    /// Creates a random refresh token that embeds its expiry.
    /// </summary>
    /// <param name="expiresAt">
    /// UTC moment when the token expires.
    /// </param>
    /// <returns>
    /// The refresh token in the form <c>random.expiryTicks</c>.
    /// </returns>
    public static string CreateRefreshToken(DateTime expiresAt)
    {
        var randomPart = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"{randomPart}.{expiresAt.Ticks}";
    }

    /// <summary>
    /// Hashes a token with SHA-256 so only the hash is stored.
    /// </summary>
    /// <param name="token">
    /// Token to hash.
    /// </param>
    /// <returns>
    /// The hash as an uppercase hexadecimal string.
    /// </returns>
    public static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>
    /// Checks that a refresh token has not expired.
    /// </summary>
    /// <param name="token">
    /// Refresh token to check.
    /// </param>
    /// <returns>
    /// <c>true</c> if the embedded expiry is in the future; otherwise <c>false</c>.
    /// </returns>
    public static bool HasValidRefreshTokenExpiry(string token)
    {
        var separator = token.LastIndexOf('.');
        return separator > 0 &&
            long.TryParse(token[(separator + 1)..], out var ticks) &&
            new DateTime(ticks, DateTimeKind.Utc) > DateTime.UtcNow;
    }
}

/// <summary>
/// Tokens returned after a successful login or refresh.
/// </summary>
/// <param name="AccessToken">
/// JWT used to authenticate requests.
/// </param>
/// <param name="RefreshToken">
/// Token used to obtain a new token pair.
/// </param>
/// <param name="ExpiresIn">
/// Access token lifetime in seconds.
/// </param>
public sealed record TokenResponse(string AccessToken, string RefreshToken, double ExpiresIn);
