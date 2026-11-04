namespace kuv_api.Models;

/// <summary>
/// One user following another. The pair is the key, so a user follows another at most once.
/// </summary>
public class Follow
{
	/// <summary>
	/// The user who follows.
	/// </summary>
	public required int FollowerId { get; set; }

	/// <summary>
	/// The user who is followed.
	/// </summary>
	public required int FollowedId { get; set; }

	/// <summary>
	/// When the user started following.
	/// </summary>
	public required DateTime CreatedAt { get; set; }

	/// <summary>
	/// The user who follows. Loaded only when a query includes it.
	/// </summary>
	public User? Follower { get; set; }

	/// <summary>
	/// The user who is followed. Loaded only when a query includes it.
	/// </summary>
	public User? Followed { get; set; }
}
