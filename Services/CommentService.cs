using kuv_api.Data;
using kuv_api.Models.Entities;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Requests;
using kuv_api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Manages comments, replies, and comment likes on user creations.
/// </summary>
public sealed class CommentService(KuvDbContext dbContext, ProfilePhotoService photos)
{
	/// <summary>
	/// Maximum allowed length of a comment in characters.
	/// </summary>
	public const int MaxCommentLength = 1000;

	private readonly KuvDbContext dbContext = dbContext;

	private readonly ProfilePhotoService photos = photos;

	/// <summary>
	/// Returns all comments and nested replies for a creation, ordered newest first.
	/// </summary>
	/// <param name="viewerId">
	/// Identifier of the signed-in user viewing the creation.
	/// </param>
	/// <param name="creationId">
	/// Identifier of the creation.
	/// </param>
	/// <returns>
	/// Top-level comments, each containing its chronological replies.
	/// </returns>
	/// <exception cref="NotFoundException">
	/// The creation does not exist.
	/// </exception>
	/// <exception cref="ForbiddenException">
	/// The creation is private and belongs to another user.
	/// </exception>
	public async Task<List<CommentResponse>> GetComments(int viewerId, int creationId)
	{
		await EnsureCreationAccessible(viewerId, creationId);

		var rawComments = await dbContext.Comments
			.Where(comment => comment.CreationId == creationId)
			.OrderBy(comment => comment.CreatedAt)
			.ThenBy(comment => comment.Id)
			.Select(comment => new
			{
				comment.Id,
				comment.CreationId,
				comment.UserId,
				comment.User.Username,
				comment.Content,
				comment.ParentCommentId,
				comment.CreatedAt
			})
			.ToListAsync();

		if (rawComments.Count == 0)
		{
			return [];
		}

		var commentIds = rawComments.Select(comment => comment.Id).ToList();

		var likeCounts = await dbContext.CommentLikes
			.Where(like => commentIds.Contains(like.CommentId))
			.GroupBy(like => like.CommentId)
			.Select(group => new
			{
				CommentId = group.Key,
				Count = group.Count()
			})
			.ToDictionaryAsync(group => group.CommentId, group => group.Count);

		var viewerLikedIds = (await dbContext.CommentLikes
			.Where(like =>
				commentIds.Contains(like.CommentId) &&
				like.UserId == viewerId
			)
			.Select(like => like.CommentId)
			.ToListAsync())
			.ToHashSet();

		var userIds = rawComments.Select(comment => comment.UserId).Distinct().ToList();
		var photoUrls = new Dictionary<int, string?>();

		foreach (var userId in userIds)
		{
			photoUrls[userId] = await photos.UrlFor(userId);
		}

		var repliesByParent = new Dictionary<int, List<CommentResponse>>();
		var topLevel = new List<CommentResponse>();

		foreach (var item in rawComments)
		{
			var response = new CommentResponse(
				Id: item.Id,
				CreationId: item.CreationId,
				UserId: item.UserId,
				Username: item.Username,
				AuthorPhoto: photoUrls[item.UserId],
				Content: item.Content,
				LikesCount: likeCounts.GetValueOrDefault(item.Id, 0),
				LikedByViewer: viewerLikedIds.Contains(item.Id),
				ParentCommentId: item.ParentCommentId,
				CreatedAt: item.CreatedAt,
				Replies: []
			);

			if (item.ParentCommentId is int parentId)
			{
				if (!repliesByParent.TryGetValue(parentId, out var list))
				{
					list = [];
					repliesByParent[parentId] = list;
				}

				list.Add(response);
			}
			else
			{
				topLevel.Add(response);
			}
		}

		var result = topLevel
			.Select(comment => comment with
			{
				Replies = repliesByParent.GetValueOrDefault(comment.Id, [])
			})
			.OrderByDescending(comment => comment.CreatedAt)
			.ThenByDescending(comment => comment.Id)
			.ToList();

		return result;
	}

	/// <summary>
	/// Posts a new comment or reply on a creation.
	/// </summary>
	/// <param name="viewerId">
	/// Identifier of the signed-in user posting the comment.
	/// </param>
	/// <param name="creationId">
	/// Identifier of the creation.
	/// </param>
	/// <param name="request">
	/// Comment text and optional parent comment id.
	/// </param>
	/// <returns>
	/// The created comment.
	/// </returns>
	/// <exception cref="BadRequestException">
	/// The comment is empty, exceeds the maximum length, or the parent comment belongs to a different creation.
	/// </exception>
	/// <exception cref="NotFoundException">
	/// The creation, user, or parent comment does not exist.
	/// </exception>
	/// <exception cref="ForbiddenException">
	/// The creation is private and belongs to another user.
	/// </exception>
	public async Task<CommentResponse> CreateComment(int viewerId, int creationId, CreateCommentRequest request)
	{
		var content = request.Content?.Trim() ?? string.Empty;

		if (string.IsNullOrWhiteSpace(content))
		{
			throw new BadRequestException("El comentario no puede estar vacío.");
		}

		if (content.Length > MaxCommentLength)
		{
			throw new BadRequestException($"El comentario no puede superar los {MaxCommentLength} caracteres.");
		}

		await EnsureCreationAccessible(viewerId, creationId);

		var user = await dbContext.Users.FirstOrDefaultAsync(user => user.Id == viewerId)
			?? throw new NotFoundException("El usuario no existe.");

		int? resolvedParentId = null;

		if (request.ParentCommentId is int parentId)
		{
			var parent = await dbContext.Comments.FirstOrDefaultAsync(comment => comment.Id == parentId)
				?? throw new NotFoundException("El comentario al que intentas responder no existe.");

			if (parent.CreationId != creationId)
			{
				throw new BadRequestException("El comentario padre no pertenece a esta creación.");
			}

			// Keep threads 1 level deep: replying to a reply attaches under its root comment
			resolvedParentId = parent.ParentCommentId ?? parent.Id;
		}

		var comment = new Comment
		{
			CreationId = creationId,
			UserId = viewerId,
			ParentCommentId = resolvedParentId,
			Content = content
		};

		dbContext.Comments.Add(comment);
		await dbContext.SaveChangesAsync();

		var authorPhoto = await photos.UrlFor(viewerId);

		return new CommentResponse(
			Id: comment.Id,
			CreationId: comment.CreationId,
			UserId: comment.UserId,
			Username: user.Username,
			AuthorPhoto: authorPhoto,
			Content: comment.Content,
			LikesCount: 0,
			LikedByViewer: false,
			ParentCommentId: comment.ParentCommentId,
			CreatedAt: comment.CreatedAt,
			Replies: []
		);
	}

	/// <summary>
	/// Deletes a comment (and its replies). Only the comment author or creation owner may delete it.
	/// </summary>
	/// <param name="viewerId">
	/// Identifier of the signed-in user requesting deletion.
	/// </param>
	/// <param name="commentId">
	/// Identifier of the comment to delete.
	/// </param>
	/// <exception cref="NotFoundException">
	/// The comment does not exist.
	/// </exception>
	/// <exception cref="ForbiddenException">
	/// The viewer is neither the comment author nor the owner of the creation.
	/// </exception>
	public async Task DeleteComment(int viewerId, int commentId)
	{
		var comment = await dbContext.Comments
			.Include(comment => comment.Creation)
			.FirstOrDefaultAsync(comment => comment.Id == commentId)
			?? throw new NotFoundException("El comentario no existe.");

		if (comment.UserId != viewerId && comment.Creation.UserId != viewerId)
		{
			throw new ForbiddenException("No tienes permiso para eliminar este comentario.");
		}

		dbContext.Comments.Remove(comment);
		await dbContext.SaveChangesAsync();
	}

	/// <summary>
	/// Adds a like from the signed-in user to a comment (idempotent).
	/// </summary>
	/// <param name="viewerId">
	/// Identifier of the signed-in user.
	/// </param>
	/// <param name="commentId">
	/// Identifier of the comment.
	/// </param>
	/// <exception cref="NotFoundException">
	/// The comment does not exist.
	/// </exception>
	/// <exception cref="ForbiddenException">
	/// The creation containing the comment is private and belongs to another user.
	/// </exception>
	public async Task LikeComment(int viewerId, int commentId)
	{
		var comment = await dbContext.Comments
			.Include(comment => comment.Creation)
			.FirstOrDefaultAsync(comment => comment.Id == commentId)
			?? throw new NotFoundException("El comentario no existe.");

		if (!comment.Creation.IsPublic && comment.Creation.UserId != viewerId)
		{
			throw new ForbiddenException("No tienes acceso a esta creación.");
		}

		var alreadyLiked = await dbContext.CommentLikes.AnyAsync(like =>
			like.CommentId == commentId &&
			like.UserId == viewerId
		);

		if (alreadyLiked)
		{
			return;
		}

		dbContext.CommentLikes.Add(new CommentLike
		{
			CommentId = commentId,
			UserId = viewerId
		});

		await dbContext.SaveChangesAsync();
	}

	/// <summary>
	/// Removes a like from the signed-in user on a comment (idempotent).
	/// </summary>
	/// <param name="viewerId">
	/// Identifier of the signed-in user.
	/// </param>
	/// <param name="commentId">
	/// Identifier of the comment.
	/// </param>
	/// <exception cref="NotFoundException">
	/// The comment does not exist.
	/// </exception>
	public async Task UnlikeComment(int viewerId, int commentId)
	{
		var exists = await dbContext.Comments.AnyAsync(comment => comment.Id == commentId);

		if (!exists)
		{
			throw new NotFoundException("El comentario no existe.");
		}

		var like = await dbContext.CommentLikes.FirstOrDefaultAsync(like =>
			like.CommentId == commentId &&
			like.UserId == viewerId
		);

		if (like is null)
		{
			return;
		}

		dbContext.CommentLikes.Remove(like);
		await dbContext.SaveChangesAsync();
	}

	private async Task EnsureCreationAccessible(int viewerId, int creationId)
	{
		var creation = await dbContext.Creations
			.Select(creation => new { creation.Id, creation.UserId, creation.IsPublic })
			.FirstOrDefaultAsync(creation => creation.Id == creationId)
			?? throw new NotFoundException("La creación no existe.");

		if (!creation.IsPublic && creation.UserId != viewerId)
		{
			throw new ForbiddenException("No tienes permiso para ver los comentarios de esta creación.");
		}
	}
}

