namespace kuv_api.Models.Responses;

/// <summary>
/// Summary of a submitted moderation report.
/// </summary>
/// <param name="Id">
/// Identifier of the report.
/// </param>
/// <param name="TargetType">
/// Type of target reported (<c>animation</c>, <c>user</c>, or <c>comment</c>).
/// </param>
/// <param name="TargetId">
/// Identifier of the reported creation, user, or comment.
/// </param>
/// <param name="ReportedUserId">
/// Identifier of the user responsible for the reported target.
/// </param>
/// <param name="Category">
/// Category code of the report.
/// </param>
/// <param name="Details">
/// Optional details provided by the reporter.
/// </param>
/// <param name="Status">
/// Current review status of the report.
/// </param>
/// <param name="CreatedAt">
/// When the report was submitted.
/// </param>
public sealed record ReportResponse(
	int Id,
	string TargetType,
	int TargetId,
	int ReportedUserId,
	string Category,
	string? Details,
	string Status,
	DateTime CreatedAt
);

