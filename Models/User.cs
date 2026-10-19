namespace kuv_api.Models;

/// <summary>
/// An account that can authenticate against the API.
/// </summary>
public class User : BaseEntity
{
	/// <summary>
	/// Unique username used to log in.
	/// </summary>
	public string Username { get; set; } = string.Empty;
	/// <summary>
	/// Unique email address.
	/// </summary>
	public string Email { get; set; } = string.Empty;
	/// <summary>
	/// Age of the user.
	/// </summary>
	public int Age { get; set; }
	/// <summary>
	/// Hashed password; the plain-text password is never stored.
	/// </summary>
	public string Hashword { get; set; } = string.Empty;
	/// <summary>
	/// Hash of the current refresh token. Null while the user has no active session.
	/// </summary>
	public string? RefreshToken { get; set; }
}