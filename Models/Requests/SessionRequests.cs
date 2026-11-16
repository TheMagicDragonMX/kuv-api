namespace kuv_api.Models.Requests;

/// <summary>
/// Credentials sent to log in.
/// </summary>
/// <param name="Username">
/// Username or email of the account. It is matched ignoring case.
/// </param>
/// <param name="Password">
/// Plain-text password of the account.
/// </param>
public sealed record LoginRequest(string Username, string Password);

/// <summary>
/// Request to exchange a refresh token for a new pair of tokens.
/// </summary>
/// <param name="RefreshToken">
/// Refresh token issued by a previous login or refresh.
/// </param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>
/// Data required to register a new user.
/// </summary>
/// <param name="Username">
/// Desired unique username.
/// </param>
/// <param name="Email">
/// Unique email address.
/// </param>
/// <param name="Age">
/// Age of the user.
/// </param>
/// <param name="Password">
/// Plain-text password; it is hashed before being stored.
/// </param>
public sealed record RegisterRequest(
    string Username,
    string Email,
    int Age,
    string Password
);

/// <summary>
/// Request to sign in with Google.
/// </summary>
/// <param name="IdToken">
/// The ID token (a JWT signed by Google) the browser got when the user chose their Google account.
/// </param>
public sealed record GoogleLoginRequest(string IdToken);

/// <summary>
/// Data that finishes creating an account that started with Google.
/// </summary>
/// <param name="SignupToken">
/// The token the API gave when it did not know the Google account.
/// </param>
/// <param name="Age">
/// Age of the user.
/// </param>
/// <param name="Username">
/// Desired unique username.
/// </param>
public sealed record GoogleSignupRequest(string SignupToken, int Age, string Username);
