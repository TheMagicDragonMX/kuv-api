using kuv_api.Models.Entities.Base;

namespace kuv_api.Models.Entities;

/// <summary>
/// A moderation report submitted by a user against an animation, a user profile, or a comment.
/// </summary>
public class Report : BaseEntity, ICreatedAt, IUpdatedAt
{
	/// <summary>
	/// Identifier of the user who submitted the report.
	/// </summary>
	public required int ReportingUserId { get; set; }

	/// <summary>
	/// Navigation property to the reporting user.
	/// </summary>
	public User ReportingUser { get; set; } = null!;

	/// <summary>
	/// Type of target being reported: <c>animation</c>, <c>user</c>, or <c>comment</c>.
	/// </summary>
	public required string TargetType { get; set; }

	/// <summary>
	/// Identifier of the reported creation, user, or comment.
	/// </summary>
	public required int TargetId { get; set; }

	/// <summary>
	/// Identifier of the user responsible for the reported content or profile.
	/// </summary>
	public required int ReportedUserId { get; set; }

	/// <summary>
	/// Navigation property to the reported user.
	/// </summary>
	public User ReportedUser { get; set; } = null!;

	/// <summary>
	/// Category code explaining the reason for the report.
	/// </summary>
	public required string Category { get; set; }

	/// <summary>
	/// Optional additional details provided by the reporting user.
	/// </summary>
	public string? Details { get; set; }

	/// <summary>
	/// Review status of the report (<c>pending</c>, <c>reviewed</c>, <c>resolved</c>, or <c>dismissed</c>).
	/// </summary>
	public string Status { get; set; } = "pending";

	/// <summary>
	/// When the report was submitted.
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// When the report was last updated.
	/// </summary>
	public DateTime UpdatedAt { get; set; }
}

