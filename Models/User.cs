namespace kuv_api.Models;

/// <summary>
/// An account that can authenticate against the API.
/// </summary>
public class User : BaseEntity
{
	/// <summary>
	/// Unique username used to log in.
	/// </summary>
	public required string Username { get; set; }
	/// <summary>
	/// Unique email address.
	/// </summary>
	public required string Email { get; set; }
	/// <summary>
	/// Age of the user.
	/// </summary>
	public required int Age { get; set; }
	/// <summary>
	/// Hashed password; the plain-text password is never stored.
	/// </summary>
	public required string Hashword { get; set; }
	/// <summary>
	/// Hash of the current refresh token. Null while the user has no active session.
	/// </summary>
	public string? RefreshToken { get; set; }
}