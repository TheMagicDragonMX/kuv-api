using kuv_api.Models.Entities.Base;

namespace kuv_api.Models.Entities;

/// <summary>
/// A comment or reply posted by a user on a creation.
/// </summary>
public class Comment : BaseEntity, ICreatedAt, IUpdatedAt
{
	/// <summary>
	/// Identifier of the creation this comment belongs to.
	/// </summary>
	public required int CreationId { get; set; }

	/// <summary>
	/// Navigation property to the creation.
	/// </summary>
	public Creation Creation { get; set; } = null!;

	/// <summary>
	/// Identifier of the user who wrote the comment.
	/// </summary>
	public required int UserId { get; set; }

	/// <summary>
	/// Navigation property to the author.
	/// </summary>
	public User User { get; set; } = null!;

	/// <summary>
	/// Optional identifier of the parent comment when this comment is a reply.
	/// </summary>
	public int? ParentCommentId { get; set; }

	/// <summary>
	/// Navigation property to the parent comment when this is a reply.
	/// </summary>
	public Comment? ParentComment { get; set; }

	/// <summary>
	/// Text content of the comment.
	/// </summary>
	public required string Content { get; set; }

	/// <summary>
	/// When the comment was created.
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// When the comment was last updated.
	/// </summary>
	public DateTime UpdatedAt { get; set; }
}

