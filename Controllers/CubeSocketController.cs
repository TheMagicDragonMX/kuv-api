using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using kuv_api.Exceptions;
using kuv_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kuv_api.Controllers;

/// <summary>
/// WebSocket endpoints for the live conversation between clients and cubes. The server only routes the messages; see
/// <see cref="CubeHub" />.
/// </summary>
[ApiController]
[Route("ws")]
public class CubeSocketController(
    CubeHub hub,
    ICubeDirectory directory
) : ControllerBase
{
    private readonly CubeHub hub = hub;

    private readonly ICubeDirectory directory = directory;

    /// <summary>
    /// Connection of a cube. The cube authenticates with its secret as a bearer token.
    /// </summary>
    /// <param name="cubeId">
    /// Identifier of the cube.
    /// </param>
    /// <exception cref="BadRequestException">
    /// The request is not a WebSocket upgrade or the cube id is missing.
    /// </exception>
    /// <exception cref="UnauthorizedException">
    /// The cube secret is wrong.
    /// </exception>
    [HttpGet("cube")]
    public async Task Cube([FromQuery] string cubeId)
    {
        EnsureWebSocket(cubeId);

        var authorization = Request.Headers.Authorization.ToString();
        var secret = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization["Bearer ".Length..] : null;

        await directory.Authenticate(cubeId, secret);

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

        await hub.RunCube(cubeId, socket, HttpContext.RequestAborted);
    }

    /// <summary>
    /// Connection of a client (the web app). Browsers cannot set headers on a WebSocket, so the access token goes in the
    /// <c>access_token</c> query parameter.
    /// </summary>
    /// <param name="cubeId">
    /// Identifier of the cube to talk to.
    /// </param>
    /// <exception cref="BadRequestException">
    /// The request is not a WebSocket upgrade or the cube id is missing.
    /// </exception>
    /// <exception cref="ForbiddenException">
    /// The user may not talk to that cube.
    /// </exception>
    [Authorize]
    [HttpGet("client")]
    public async Task Client([FromQuery] string cubeId)
    {
        EnsureWebSocket(cubeId);

        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(subject, out var userId))
        {
            throw new UnauthorizedException("The token has no valid user.");
        }

        await directory.EnsureAccess(userId, cubeId);

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();

        await hub.RunClient(cubeId, socket, HttpContext.RequestAborted);
    }

    /// <summary>
    /// Rejects requests that cannot become a socket.
    /// </summary>
    private void EnsureWebSocket(string cubeId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            throw new BadRequestException("This endpoint only accepts WebSocket connections.");
        }

        if (string.IsNullOrWhiteSpace(cubeId))
        {
            throw new BadRequestException("cubeId is required.");
        }
    }
}
