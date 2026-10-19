using kuv_api.Data;
using kuv_api.Models;
using kuv_api.Requests;
using kuv_api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace kuv_api.Controllers;

/// <summary>
/// Handles registration, login and token refresh.
/// </summary>
[ApiController]
[Route("session")]
public class SessionController(
    KuvDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    UserService userService,
    TokenService tokenService
) : ControllerBase
{
    private readonly KuvDbContext dbContext = dbContext;

    private readonly IPasswordHasher<User> passwordHasher = passwordHasher;

    private readonly UserService userService = userService;

    private readonly TokenService tokenService = tokenService;

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="request">
    /// Registration data.
    /// </param>
    /// <returns>
    /// 201 Created with the created user.
    /// </returns>
    /// <exception cref="ConflictException">
    /// The username or email is already registered.
    /// </exception>
    [HttpPost("register")]
    public async Task<ActionResult<RegisteredUserResponse>> Register(RegisterRequest request)
    {
        var user = await userService.Register(request);

        return StatusCode(StatusCodes.Status201Created, new RegisteredUserResponse(user.Id, user.Username, user.Email, user.Age));
    }

    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    /// <param name="request">
    /// Login credentials.
    /// </param>
    /// <returns>
    /// 200 OK with the issued tokens, or 401 Unauthorized if the credentials are wrong.
    /// </returns>
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            user => user.Username == request.Username);

        if (user is null || passwordHasher.VerifyHashedPassword(user, user.Hashword, request.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        return Ok(await tokenService.IssueTokens(user));
    }

    /// <summary>
    /// Issues a new token pair from a valid refresh token.
    /// </summary>
    /// <param name="request">
    /// Refresh token to exchange.
    /// </param>
    /// <returns>
    /// 200 OK with the issued tokens, or 401 Unauthorized if the token is unknown or expired.
    /// </returns>
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request)
    {
        var refreshTokenHash = TokenService.HashToken(request.RefreshToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            user => user.RefreshToken == refreshTokenHash);

        if (user is null || !TokenService.HasValidRefreshTokenExpiry(request.RefreshToken))
        {
            return Unauthorized();
        }

        return Ok(await tokenService.IssueTokens(user));
    }
}

/// <summary>
/// Public data of a newly registered user.
/// </summary>
/// <param name="Id">
/// Identifier of the user.
/// </param>
/// <param name="Username">
/// Username of the user.
/// </param>
/// <param name="Email">
/// Email of the user.
/// </param>
/// <param name="Age">
/// Age of the user.
/// </param>
public sealed record RegisteredUserResponse(int Id, string Username, string Email, int Age);