using Google.Apis.Auth;
using kuv_api.Data;
using kuv_api.Models.Entities;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Requests;
using kuv_api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Signs users in with Google. The browser gets an ID token from Google; this service checks it, then either
/// signs in the user it belongs to (linking the Google account to the user with the same email, if there
/// is one) or tells the client what is missing to create the account: the age and the username.
/// Whoever is signed in gets the API's own tokens, the same as with a password.
/// </summary>
public sealed class GoogleAuthService(
    KuvDbContext dbContext,
    TokenService tokenService,
    UserService userService,
    IConfiguration configuration
)
{
    private readonly KuvDbContext dbContext = dbContext;

    private readonly TokenService tokenService = tokenService;

    private readonly UserService userService = userService;

    private readonly IConfiguration configuration = configuration;

    /// <summary>
    /// Signs in with the ID token Google gave the browser.
    /// </summary>
    /// <param name="request">
    /// The ID token.
    /// </param>
    /// <returns>
    /// The session when the Google account belongs to a user (found by its Google id, or by its email, which
    /// links the two), or a signup token when it does not.
    /// </returns>
    /// <exception cref="UnauthorizedException">
    /// The token is not valid, or Google has not verified the email.
    /// </exception>
    /// <exception cref="ConflictException">
    /// The email belongs to a user linked to another Google account.
    /// </exception>
    public async Task<GoogleSessionResponse> SignIn(GoogleLoginRequest request)
    {
        var identity = await Verify(request.IdToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(user => user.GoogleId == identity.Subject);

        if (user is null)
        {
            user = await dbContext.Users.FirstOrDefaultAsync(user => user.Email.ToLower() == identity.Email.ToLower());

            if (user is not null)
            {
                if (user.GoogleId is not null)
                {
                    throw new ConflictException("Ese correo ya está vinculado a otra cuenta de Google.");
                }

                // Same verified email: the account is the user's, so it is linked (saved together with the tokens)
                user.GoogleId = identity.Subject;
            }
        }

        if (user is null)
        {
            return new GoogleSessionResponse
            {
                Status = GoogleSessionResponse.NeedsProfile,
                SignupToken = tokenService.IssueGoogleSignupToken(identity),
                Email = identity.Email
            };
        }

        return new GoogleSessionResponse
        {
            Status = GoogleSessionResponse.SignedIn,
            Tokens = await tokenService.IssueTokens(user)
        };
    }

    /// <summary>
    /// Creates the account of a Google user once they have chosen their age and username, and signs them in.
    /// The account has no password.
    /// </summary>
    /// <param name="request">
    /// The signup token and the data to create the account with.
    /// </param>
    /// <returns>
    /// The tokens of the new session.
    /// </returns>
    /// <exception cref="UnauthorizedException">
    /// The signup token is not valid or has expired.
    /// </exception>
    /// <exception cref="BadRequestException">
    /// The age or the username is out of range.
    /// </exception>
    /// <exception cref="ConflictException">
    /// The username is taken, or the Google account or its email is already registered.
    /// </exception>
    public async Task<TokenResponse> CompleteSignUp(GoogleSignupRequest request)
    {
        var identity = tokenService.ReadGoogleSignupToken(request.SignupToken);
        var username = request.Username.Trim();

        UserService.CheckAge(request.Age);

        var alreadyRegistered = await dbContext.Users.AnyAsync(user =>
            user.GoogleId == identity.Subject ||
            user.Email.ToLower() == identity.Email.ToLower()
        );

        if (alreadyRegistered)
        {
            throw new ConflictException("Esta cuenta de Google ya está registrada. Inicia sesión con ella.");
        }

        if (!await userService.IsUsernameAvailable(username))
        {
            throw new ConflictException("Ese nombre de usuario ya está en uso.");
        }

        var user = new User
        {
            Username = username,
            Email = identity.Email,
            Age = request.Age,
            GoogleId = identity.Subject
        };

        // Saved first: the database gives the id, and the access token names the user by it
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return await tokenService.IssueTokens(user);
    }

    /// <summary>
    /// Checks the ID token with Google (signature, issuer, audience, expiry) and that it has a verified email.
    /// </summary>
    /// <param name="idToken">
    /// The ID token to check.
    /// </param>
    /// <returns>
    /// The Google account the token belongs to.
    /// </returns>
    /// <exception cref="UnauthorizedException">
    /// The token is not valid, or Google has not verified the email.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <c>Google:ClientId</c> is not configured.
    /// </exception>
    private async Task<GoogleIdentity> Verify(string idToken)
    {
        var clientId = configuration["Google:ClientId"];

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Google:ClientId must be configured to sign in with Google.");
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new UnauthorizedException("No se pudo verificar tu cuenta de Google.");
        }

        GoogleJsonWebSignature.Payload payload;

        try
        {
            // The audience must be this app's client id, or a token Google made for another app would be accepted
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            });
        }
        catch (Exception exception) when (exception is InvalidJwtException or Newtonsoft.Json.JsonException or FormatException)
        {
            // The library reports a token that is not even a JWT with the parsing error, not with InvalidJwtException
            throw new UnauthorizedException("No se pudo verificar tu cuenta de Google.");
        }

        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new UnauthorizedException("Google no ha verificado el correo de esa cuenta.");
        }

        return new GoogleIdentity(payload.Subject, payload.Email.Trim());
    }
}

/// <summary>
/// A Google account whose ID token was verified.
/// </summary>
/// <param name="Subject">
/// Identifier of the Google account (the <c>sub</c> of its ID token); it never changes.
/// </param>
/// <param name="Email">
/// Email of the Google account, which Google has verified.
/// </param>
public sealed record GoogleIdentity(string Subject, string Email);
