namespace kuv_api.Models.Requests;

/// <summary>
/// Data needed to post a new comment or reply on a creation.
/// </summary>
public class CreateCommentRequest
{
	/// <summary>
	/// Text content of the comment.
	/// </summary>
	public required string Content { get; set; }

	/// <summary>
	/// Optional identifier of the parent comment when replying to another comment.
	/// </summary>
	public int? ParentCommentId { get; set; }
}

