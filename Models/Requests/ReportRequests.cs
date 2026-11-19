namespace kuv_api.Models.Requests;

/// <summary>
/// Data needed to submit a moderation report against an animation, user profile, or comment.
/// </summary>
public class CreateReportRequest
{
	/// <summary>
	/// Type of target being reported: <c>animation</c>, <c>user</c>, or <c>comment</c>.
	/// </summary>
	public required string TargetType { get; set; }

	/// <summary>
	/// Identifier of the creation, user, or comment being reported.
	/// </summary>
	public required int TargetId { get; set; }

	/// <summary>
	/// Category code explaining the reason for the report.
	/// </summary>
	public required string Category { get; set; }

	/// <summary>
	/// Optional explanation or additional details (required when category is <c>other</c>).
	/// </summary>
	public string? Details { get; set; }
}

