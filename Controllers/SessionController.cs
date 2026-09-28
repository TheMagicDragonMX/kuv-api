using kuv_api.Data;
using kuv_api.Models;
using kuv_api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Controllers;

[ApiController]
[Route("session")]
public class SessionController(
    KuvDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    TokenService tokenService
) : ControllerBase
{
    private readonly KuvDbContext dbContext = dbContext;
    private readonly IPasswordHasher<User> passwordHasher = passwordHasher;
    private readonly TokenService tokenService = tokenService;

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

        return Ok(await tokenService.IssueTokens(user, cancellationToken));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var refreshTokenHash = TokenService.HashToken(request.RefreshToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.RefreshToken == refreshTokenHash,
            cancellationToken);

        if (user is null || !TokenService.HasValidRefreshTokenExpiry(request.RefreshToken))
        {
            return Unauthorized();
        }

        return Ok(await tokenService.IssueTokens(user, cancellationToken));
    }

    public sealed record LoginRequest(string Username, string Password);
    public sealed record RefreshRequest(string RefreshToken);
}