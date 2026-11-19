using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Requests;
using kuv_api.Models.Responses;
using kuv_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kuv_api.Controllers;

/// <summary>
/// Endpoints for reading, posting, deleting, and liking comments on creations.
/// </summary>
[ApiController]
[Authorize]
public class CommentsController(CommentService commentService) : ControllerBase
{
	private readonly CommentService commentService = commentService;

	/// <summary>
	/// Returns all comments and nested replies for a creation.
	/// </summary>
	/// <param name="creationId">
	/// Identifier of the creation.
	/// </param>
	/// <returns>
	/// 200 OK with the list of top-level comments and their replies.
	/// </returns>
	[HttpGet("creations/{creationId:int}/comments")]
	public async Task<ActionResult<List<CommentResponse>>> GetComments(int creationId) =>
		Ok(await commentService.GetComments(ViewerId(), creationId));

	/// <summary>
	/// Posts a new comment or reply on a creation.
	/// </summary>
	/// <param name="creationId">
	/// Identifier of the creation.
	/// </param>
	/// <param name="request">
	/// Comment content and optional parent comment id.
	/// </param>
	/// <returns>
	/// 201 Created with the newly created comment.
	/// </returns>
	[HttpPost("creations/{creationId:int}/comments")]
	public async Task<ActionResult<CommentResponse>> CreateComment(int creationId, CreateCommentRequest request)
	{
		var created = await commentService.CreateComment(ViewerId(), creationId, request);
		return Created($"creations/{creationId}/comments/{created.Id}", created);
	}

	/// <summary>
	/// Deletes a comment (only allowed for the comment author or creation owner).
	/// </summary>
	/// <param name="commentId">
	/// Identifier of the comment to delete.
	/// </param>
	/// <returns>
	/// 204 No Content.
	/// </returns>
	[HttpDelete("comments/{commentId:int}")]
	public async Task<IActionResult> DeleteComment(int commentId)
	{
		await commentService.DeleteComment(ViewerId(), commentId);
		return NoContent();
	}

	/// <summary>
	/// Adds a like from the signed-in user to a comment.
	/// </summary>
	/// <param name="commentId">
	/// Identifier of the comment.
	/// </param>
	/// <returns>
	/// 204 No Content.
	/// </returns>
	[HttpPut("comments/{commentId:int}/like")]
	public async Task<IActionResult> LikeComment(int commentId)
	{
		await commentService.LikeComment(ViewerId(), commentId);
		return NoContent();
	}

	/// <summary>
	/// Removes a like from the signed-in user on a comment.
	/// </summary>
	/// <param name="commentId">
	/// Identifier of the comment.
	/// </param>
	/// <returns>
	/// 204 No Content.
	/// </returns>
	[HttpDelete("comments/{commentId:int}/like")]
	public async Task<IActionResult> UnlikeComment(int commentId)
	{
		await commentService.UnlikeComment(ViewerId(), commentId);
		return NoContent();
	}

	private int ViewerId()
	{
		var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

		if (!int.TryParse(subject, out var userId))
		{
			throw new UnauthorizedException("The token has no valid user.");
		}

		return userId;
	}
}

