using System.Security.Cryptography;
using System.Text;
using kuv_api.Exceptions;

namespace kuv_api.Services;

/// <summary>
/// Cube directory without a database: every cube shares the secret in <c>Cubes:Secret</c> and every signed-in user may
/// talk to every cube. Replace it with one that reads the stored devices when they exist.
/// </summary>
public class ConfigCubeDirectory(IConfiguration configuration) : ICubeDirectory
{
    private readonly IConfiguration configuration = configuration;

    /// <inheritdoc />
    public Task Authenticate(string cubeId, string? secret)
    {
        var expected = configuration["Cubes:Secret"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            throw new UnauthorizedException("Cubes:Secret is not configured, so no cube can connect.");
        }

        var matches = secret is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(secret)
        );

        if (!matches)
        {
            throw new UnauthorizedException("Wrong cube secret.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureAccess(int userId, string cubeId)
    {
        return Task.CompletedTask;
    }
}
