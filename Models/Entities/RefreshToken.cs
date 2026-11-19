using kuv_api.Models.Entities.Base;

namespace kuv_api.Models.Entities;

/// <summary>
/// One signed-in session of a user: the refresh token a device holds. A user has one per device (or teammate) signed
/// in, so signing in somewhere new does not sign anyone else out. Only the hash of the token is stored.
/// </summary>
public class RefreshToken : BaseEntity, ICreatedAt
{
	/// <summary>
	/// The user the session belongs to.
	/// </summary>
	public required int UserId { get; set; }

	/// <summary>
	/// The user the session belongs to. Loaded only when a query includes it.
	/// </summary>
	public User User { get; set; } = null!;

	/// <summary>
	/// SHA-256 hash of the refresh token (see <c>TokenService.HashToken</c>); the token itself is never stored.
	/// </summary>
	public required string TokenHash { get; set; }

	/// <summary>
	/// When the token stops working. Each refresh replaces the row with a new one that lasts the whole lifetime again.
	/// </summary>
	public required DateTime ExpiresAt { get; set; }

	/// <summary>
	/// When the token was issued.
	/// </summary>
	public DateTime CreatedAt { get; set; }
}
