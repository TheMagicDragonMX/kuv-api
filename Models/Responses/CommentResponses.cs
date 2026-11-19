namespace kuv_api.Models.Responses;

/// <summary>
/// A comment on a creation, with its author, like count, and nested replies.
/// </summary>
/// <param name="Id">
/// Identifier of the comment.
/// </param>
/// <param name="CreationId">
/// Identifier of the creation the comment belongs to.
/// </param>
/// <param name="UserId">
/// Identifier of the user who wrote the comment.
/// </param>
/// <param name="Username">
/// Username of the user who wrote the comment.
/// </param>
/// <param name="AuthorPhoto">
/// Where the profile picture of the author can be fetched from, or null when they have none.
/// </param>
/// <param name="Content">
/// Text content of the comment.
/// </param>
/// <param name="LikesCount">
/// Number of likes the comment has received.
/// </param>
/// <param name="LikedByViewer">
/// Whether the signed-in viewer has liked this comment.
/// </param>
/// <param name="ParentCommentId">
/// Identifier of the parent comment when this is a reply, or null for top-level comments.
/// </param>
/// <param name="CreatedAt">
/// When the comment was posted.
/// </param>
/// <param name="Replies">
/// Replies to this comment, ordered chronologically.
/// </param>
public sealed record CommentResponse(
	int Id,
	int CreationId,
	int UserId,
	string Username,
	string? AuthorPhoto,
	string Content,
	int LikesCount,
	bool LikedByViewer,
	int? ParentCommentId,
	DateTime CreatedAt,
	List<CommentResponse> Replies
);

