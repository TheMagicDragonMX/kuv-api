using kuv_api.Data;
using kuv_api.Models.Entities;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Requests;
using kuv_api.Models.Responses;
using kuv_api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace kuv_api.Controllers;

/// <summary>
/// Handles registration, login (with a password or with Google) and token refresh.
/// </summary>
[ApiController]
[Route("session")]
public class SessionController(
    KuvDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    UserService userService,
    TokenService tokenService,
    GoogleAuthService googleAuthService
) : ControllerBase
{
    private readonly KuvDbContext dbContext = dbContext;

    private readonly IPasswordHasher<User> passwordHasher = passwordHasher;

    private readonly UserService userService = userService;

    private readonly TokenService tokenService = tokenService;

    private readonly GoogleAuthService googleAuthService = googleAuthService;

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
    /// The email or the username is already registered; the message says which.
    /// </exception>
    [HttpPost("register")]
    public async Task<ActionResult<RegisteredUserResponse>> Register(RegisterRequest request)
    {
        var user = await userService.Register(request);

        return StatusCode(StatusCodes.Status201Created, new RegisteredUserResponse(user.Id, user.Username, user.Email, user.Age));
    }

    /// <summary>
    /// Authenticates a user with their username or email and a password.
    /// </summary>
    /// <param name="request">
    /// Login credentials; the username field accepts either the username or the email.
    /// </param>
    /// <returns>
    /// 200 OK with the issued tokens, or 401 Unauthorized if the credentials are wrong.
    /// </returns>
    /// <exception cref="UnauthorizedException">
    /// The account was created with Google and has no password.
    /// </exception>
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var identifier = request.Username.Trim().ToLower();

        // A username wins over an email that spells the same, so the answer does not depend on the order of the rows
        var user = await dbContext.Users
            .Where(user =>
                user.Username.ToLower() == identifier ||
                user.Email.ToLower() == identifier
            )
            .OrderBy(user => user.Username.ToLower() == identifier ? 0 : 1)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            return Unauthorized();
        }

        if (user.Hashword is null)
        {
            throw new UnauthorizedException("Esta cuenta usa Google. Entra con el botón de Google.");
        }

        if (passwordHasher.VerifyHashedPassword(user, user.Hashword, request.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        return Ok(await tokenService.IssueTokens(user));
    }

    /// <summary>
    /// Signs in with Google.
    /// </summary>
    /// <param name="request">
    /// The ID token Google gave the browser.
    /// </param>
    /// <returns>
    /// 200 OK with the session, or with a signup token when the Google account has no user yet and the age and
    /// username are still needed (see <see cref="Complete"/>).
    /// </returns>
    [HttpPost("google")]
    public async Task<ActionResult<GoogleSessionResponse>> Google(GoogleLoginRequest request)
    {
        return Ok(await googleAuthService.SignIn(request));
    }

    /// <summary>
    /// Creates the account of a Google user with the age and username they chose, and signs them in.
    /// </summary>
    /// <param name="request">
    /// The signup token and the data of the account.
    /// </param>
    /// <returns>
    /// 200 OK with the issued tokens.
    /// </returns>
    [HttpPost("google/complete")]
    public async Task<ActionResult<TokenResponse>> Complete(GoogleSignupRequest request)
    {
        return Ok(await googleAuthService.CompleteSignUp(request));
    }

    /// <summary>
    /// Tells whether a username is free, so the sign up can say so before it is sent.
    /// </summary>
    /// <param name="username">
    /// The username to look for.
    /// </param>
    /// <returns>
    /// 200 OK saying whether it is available.
    /// </returns>
    [HttpGet("username-available")]
    public async Task<ActionResult<AvailabilityResponse>> UsernameAvailable([FromQuery] string username)
    {
        return Ok(new AvailabilityResponse(await userService.IsUsernameAvailable(username)));
    }

    /// <summary>
    /// Tells whether an email is free, so the sign up can say so before asking for anything else.
    /// </summary>
    /// <param name="email">
    /// The email to look for.
    /// </param>
    /// <returns>
    /// 200 OK saying whether it is available.
    /// </returns>
    [HttpGet("email-available")]
    public async Task<ActionResult<AvailabilityResponse>> EmailAvailable([FromQuery] string email)
    {
        return Ok(new AvailabilityResponse(await userService.IsEmailAvailable(email)));
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
        var user = await dbContext.Users.SingleOrDefaultAsync(user => user.RefreshToken == refreshTokenHash);

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