using kuv_api.Data;
using kuv_api.Models;
using kuv_api.Requests;
using kuv_api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace kuv_api.Controllers;

[ApiController]
[Route("session")]
public class SessionController(
    KuvDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    UserRegistrationService userRegistrationService,
    TokenService tokenService
) : ControllerBase
{
    private readonly KuvDbContext dbContext = dbContext;
    private readonly IPasswordHasher<User> passwordHasher = passwordHasher;
    private readonly UserRegistrationService userRegistrationService = userRegistrationService;
    private readonly TokenService tokenService = tokenService;

    [HttpPost("register")]
    public async Task<ActionResult<RegisteredUserResponse>> Register(RegisterRequest request)
    {
        var user = await userRegistrationService.RegisterAsync(request);

        if (user is null)
        {
            return Conflict(new { message = "El nombre de usuario o correo ya está registrado." });
        }

        return StatusCode(StatusCodes.Status201Created,
            new RegisteredUserResponse(user.Id, user.Username, user.Email, user.Age));
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Username == request.Username);

        if (user is null || passwordHasher.VerifyHashedPassword(user, user.Hashword, request.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        return Ok(await tokenService.IssueTokens(user));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request)
    {
        var refreshTokenHash = TokenService.HashToken(request.RefreshToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.RefreshToken == refreshTokenHash);

        if (user is null || !TokenService.HasValidRefreshTokenExpiry(request.RefreshToken))
        {
            return Unauthorized();
        }

        return Ok(await tokenService.IssueTokens(user));
    }
}

public sealed record RegisteredUserResponse(int Id, string Username, string Email, int Age);