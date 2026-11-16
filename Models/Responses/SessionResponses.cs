using kuv_api.Services;

namespace kuv_api.Models.Responses;

/// <summary>
/// What the API answers to a sign in with Google: either the session, or what is still needed to create the account.
/// </summary>
public sealed record GoogleSessionResponse
{
	/// <summary>
	/// Status when the Google account belongs to a user and the session is ready.
	/// </summary>
	public const string SignedIn = "signedIn";

	/// <summary>
	/// Status when the Google account has no user yet and the age and username are still needed.
	/// </summary>
	public const string NeedsProfile = "needsProfile";

	/// <summary>
	/// <see cref="SignedIn"/> or <see cref="NeedsProfile"/>.
	/// </summary>
	public required string Status { get; init; }

	/// <summary>
	/// The tokens of the session. Null unless the status is <see cref="SignedIn"/>.
	/// </summary>
	public TokenResponse? Tokens { get; init; }

	/// <summary>
	/// Token to send back with the age and username. Null unless the status is <see cref="NeedsProfile"/>.
	/// </summary>
	public string? SignupToken { get; init; }

	/// <summary>
	/// Email of the Google account, to tell the user which account they are using. Null unless the status is <see cref="NeedsProfile"/>.
	/// </summary>
	public string? Email { get; init; }
}

/// <summary>
/// Whether a username can still be taken.
/// </summary>
/// <param name="Available">
/// True when no user has the username.
/// </param>
public sealed record UsernameAvailabilityResponse(bool Available);
