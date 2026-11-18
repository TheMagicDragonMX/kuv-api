using kuv_api.Models.Entities.Base;

namespace kuv_api.Models.Entities;

/// <summary>
/// A like given by a user to a comment.
/// </summary>
public class CommentLike : ICreatedAt
{
	/// <summary>
	/// Identifier of the liked comment.
	/// </summary>
	public required int CommentId { get; set; }

	/// <summary>
	/// Navigation property to the liked comment.
	/// </summary>
	public Comment Comment { get; set; } = null!;

	/// <summary>
	/// Identifier of the user who liked the comment.
	/// </summary>
	public required int UserId { get; set; }

	/// <summary>
	/// Navigation property to the user who liked the comment.
	/// </summary>
	public User User { get; set; } = null!;

	/// <summary>
	/// When the like was given.
	/// </summary>
	public DateTime CreatedAt { get; set; }
}

