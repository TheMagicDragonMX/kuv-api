using kuv_api.Exceptions;

namespace kuv_api.Services;

/// <summary>
/// Knows which cubes exist and who may use them. Today it is backed by configuration; once the devices are stored,
/// another implementation replaces it without touching the sockets.
/// </summary>
public interface ICubeDirectory
{
    /// <summary>
    /// Checks that a cube connecting to the server is who it says it is.
    /// </summary>
    /// <param name="cubeId">
    /// Identifier the cube claims.
    /// </param>
    /// <param name="secret">
    /// Secret the cube presented.
    /// </param>
    /// <exception cref="UnauthorizedException">
    /// The secret is missing or wrong.
    /// </exception>
    Task Authenticate(string cubeId, string? secret);

    /// <summary>
    /// Checks that a user may talk to a cube.
    /// </summary>
    /// <param name="userId">
    /// Identifier of the user.
    /// </param>
    /// <param name="cubeId">
    /// Identifier of the cube.
    /// </param>
    /// <exception cref="ForbiddenException">
    /// The user does not own the cube.
    /// </exception>
    Task EnsureAccess(int userId, string cubeId);
}
