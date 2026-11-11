using kuv_api.Data;
using kuv_api.Exceptions;
using kuv_api.Models;
using kuv_api.Requests;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Manages user creations and video files on disk.
/// </summary>
public sealed class CreationService(KuvDbContext db, IWebHostEnvironment environment)
{
	private readonly KuvDbContext db = db;
	private readonly string storageDirectory = Path.Combine(environment.ContentRootPath, "uploads", "creations");

	/// <summary>
	/// Gets the full path and content type for a creation's video file.
	/// </summary>
	public async Task<(string FilePath, string ContentType)> GetCreationVideo(int viewerId, int creationId)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		if (creation.UserId != viewerId && !creation.IsPublic)
		{
			throw new ForbiddenException("You cannot access this creation.");
		}

		if (string.IsNullOrWhiteSpace(creation.VideoFileName))
		{
			throw new NotFoundException("This creation does not have a video file.");
		}

		var fullPath = Path.Combine(storageDirectory, creation.VideoFileName);
		if (!File.Exists(fullPath))
		{
			throw new NotFoundException("The video file was not found on the server.");
		}

		var contentType = !string.IsNullOrWhiteSpace(creation.VideoMimeType)
			? creation.VideoMimeType
			: "video/webm";

		return (fullPath, contentType);
	}

	/// <summary>
	/// Returns creations of the specified user.
	/// If viewerId == userId, returns all creations.
	/// If viewerId != userId, returns only public creations.
	/// </summary>
	public async Task<List<CreationResponse>> GetUserCreations(int viewerId, int userId)
	{
		var query = db.Creations.Where(c => c.UserId == userId);

		if (viewerId != userId)
		{
			query = query.Where(c => c.IsPublic);
		}

		return await query
			.OrderByDescending(c => c.CreatedAt)
			.Select(c => ToResponse(c))
			.ToListAsync();
	}

	/// <summary>
	/// Returns all creations of the specified user.
	/// </summary>
	public async Task<List<CreationResponse>> GetUserCreations(int userId) =>
		await GetUserCreations(userId, userId);

	/// <summary>
	/// Returns a specific creation by ID if owned by the viewer or if public.
	/// </summary>
	public async Task<CreationResponse> GetCreation(int viewerId, int creationId)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		if (creation.UserId != viewerId && !creation.IsPublic)
		{
			throw new ForbiddenException("You cannot access this creation.");
		}

		return ToResponse(creation);
	}

	/// <summary>
	/// Creates a new creation and optionally stores its video file.
	/// </summary>
	public async Task<CreationResponse> Create(int userId, CreateCreationRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Title))
		{
			throw new BadRequestException("Title is required.");
		}

		if (string.IsNullOrWhiteSpace(request.ProgramJson))
		{
			throw new BadRequestException("ProgramJson is required.");
		}

		var now = DateTime.UtcNow;
		var creation = new Creation
		{
			UserId = userId,
			Title = request.Title.Trim(),
			Description = request.Description?.Trim(),
			ProgramJson = request.ProgramJson,
			EnvironmentJson = request.EnvironmentJson,
			DurationSeconds = request.DurationSeconds,
			IsPublic = request.IsPublic,
			CreatedAt = now,
			UpdatedAt = now,
		};

		db.Creations.Add(creation);
		await db.SaveChangesAsync();

		if (request.Video != null && 0 < request.Video.Length)
		{
			creation.VideoFileName = await SaveVideoFile(creation.Id, request.Video);
			creation.VideoMimeType = request.Video.ContentType ?? "video/webm";
			await db.SaveChangesAsync();
		}

		return ToResponse(creation);
	}

	/// <summary>
	/// Updates an existing creation.
	/// </summary>
	public async Task<CreationResponse> Update(int userId, int creationId, UpdateCreationRequest request)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		if (creation.UserId != userId)
		{
			throw new ForbiddenException("You are not allowed to edit this creation.");
		}

		if (!string.IsNullOrWhiteSpace(request.Title))
		{
			creation.Title = request.Title.Trim();
		}

		creation.Description = request.Description?.Trim();

		if (!string.IsNullOrWhiteSpace(request.ProgramJson))
		{
			creation.ProgramJson = request.ProgramJson;
		}

		if (request.EnvironmentJson != null)
		{
			creation.EnvironmentJson = request.EnvironmentJson;
		}

		if (0 < request.DurationSeconds)
		{
			creation.DurationSeconds = request.DurationSeconds;
		}

		if (request.IsPublic.HasValue)
		{
			creation.IsPublic = request.IsPublic.Value;
		}

		if (request.Video != null && 0 < request.Video.Length)
		{
			DeleteVideoFile(creation.VideoFileName);
			creation.VideoFileName = await SaveVideoFile(creation.Id, request.Video);
			creation.VideoMimeType = request.Video.ContentType ?? "video/webm";
		}

		creation.UpdatedAt = DateTime.UtcNow;
		await db.SaveChangesAsync();

		return ToResponse(creation);
	}

	/// <summary>
	/// Sets the public visibility of a creation.
	/// </summary>
	public async Task<CreationResponse> SetVisibility(int userId, int creationId, bool isPublic)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		if (creation.UserId != userId)
		{
			throw new ForbiddenException("You are not allowed to edit this creation.");
		}

		creation.IsPublic = isPublic;
		creation.UpdatedAt = DateTime.UtcNow;
		await db.SaveChangesAsync();

		return ToResponse(creation);
	}

	/// <summary>
	/// Deletes a creation and its associated video file.
	/// </summary>
	public async Task Delete(int userId, int creationId)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		if (creation.UserId != userId)
		{
			throw new ForbiddenException("You are not allowed to delete this creation.");
		}

		DeleteVideoFile(creation.VideoFileName);
		db.Creations.Remove(creation);
		await db.SaveChangesAsync();
	}

	private async Task<string> SaveVideoFile(int creationId, IFormFile file)
	{
		Directory.CreateDirectory(storageDirectory);
		var extension = Path.GetExtension(file.FileName);
		if (string.IsNullOrWhiteSpace(extension))
		{
			extension = ".webm";
		}

		var fileName = $"{creationId}_{Guid.NewGuid():N}{extension}";
		var fullPath = Path.Combine(storageDirectory, fileName);

		using var stream = File.Create(fullPath);
		await file.CopyToAsync(stream);

		return fileName;
	}

	private void DeleteVideoFile(string? fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			return;
		}

		try
		{
			var fullPath = Path.Combine(storageDirectory, fileName);
			if (File.Exists(fullPath))
			{
				File.Delete(fullPath);
			}
		}
		catch
		{
			// Best-effort cleanup
		}
	}

	private static CreationResponse ToResponse(Creation creation)
	{
		return new CreationResponse(
			creation.Id,
			creation.UserId,
			creation.Title,
			creation.Description,
			creation.ProgramJson,
			creation.EnvironmentJson,
			creation.DurationSeconds,
			!string.IsNullOrWhiteSpace(creation.VideoFileName),
			creation.IsPublic,
			creation.CreatedAt,
			creation.UpdatedAt
		);
	}
}

