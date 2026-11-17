using kuv_api.Data;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Entities;
using kuv_api.Models.Requests;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Manages user creations: what describes them is in the database, and each one has its own folder on disk with its
/// program file and its video (see <see cref="CreationStorage"/>).
/// </summary>
public sealed class CreationService(KuvDbContext db, CreationStorage storage, ProgramFileValidator validator)
{
	private readonly KuvDbContext db = db;

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

		var fullPath = storage.VideoPath(creation.Id, creation.VideoFileName);
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
	public async Task<CreationDetailResponse> GetCreation(int viewerId, int creationId)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		if (creation.UserId != viewerId && !creation.IsPublic)
		{
			throw new ForbiddenException("You cannot access this creation.");
		}

		return await ToDetailResponse(creation);
	}

	/// <summary>
	/// Creates a new creation: a row in the database, a folder with its program file and, when given, its video.
	/// </summary>
	public async Task<CreationDetailResponse> Create(int userId, CreateCreationRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Title))
		{
			throw new BadRequestException("Title is required.");
		}

		// Checked before anything is stored, so a bad program leaves no row and no folder behind
		var programFile = validator.Parse(request.ProgramFile);

		var creation = new Creation
		{
			UserId = userId,
			Title = request.Title.Trim(),
			Description = request.Description?.Trim(),
			DurationSeconds = request.DurationSeconds,
			IsPublic = request.IsPublic,
		};

		db.Creations.Add(creation);
		await db.SaveChangesAsync();

		try
		{
			await storage.WriteProgramFile(creation.Id, programFile);

			if (request.Video != null && 0 < request.Video.Length)
			{
				creation.VideoFileName = await storage.WriteVideo(creation.Id, request.Video);
				creation.VideoMimeType = request.Video.ContentType ?? "video/webm";
				await db.SaveChangesAsync();
			}
		}

		catch
		{
			// A creation without its files would be unusable, so it is taken back
			storage.DeleteFolder(creation.Id);
			db.Creations.Remove(creation);
			await db.SaveChangesAsync();

			throw;
		}

		return new CreationDetailResponse(ToResponse(creation), programFile);
	}

	/// <summary>
	/// Updates an existing creation.
	/// </summary>
	public async Task<CreationDetailResponse> Update(int userId, int creationId, UpdateCreationRequest request)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		if (creation.UserId != userId)
		{
			throw new ForbiddenException("You are not allowed to edit this creation.");
		}

		// Checked before anything changes, so a bad program leaves the creation as it was
		var programFile = string.IsNullOrWhiteSpace(request.ProgramFile) ? null : validator.Parse(request.ProgramFile);

		if (!string.IsNullOrWhiteSpace(request.Title))
		{
			creation.Title = request.Title.Trim();
		}

		creation.Description = request.Description?.Trim();

		if (0 < request.DurationSeconds)
		{
			creation.DurationSeconds = request.DurationSeconds;
		}

		if (request.IsPublic.HasValue)
		{
			creation.IsPublic = request.IsPublic.Value;
		}

		if (programFile != null)
		{
			await storage.WriteProgramFile(creation.Id, programFile);
		}

		if (request.Video != null && 0 < request.Video.Length)
		{
			creation.VideoFileName = await storage.WriteVideo(creation.Id, request.Video);
			creation.VideoMimeType = request.Video.ContentType ?? "video/webm";
		}

		await db.SaveChangesAsync();

		return await ToDetailResponse(creation);
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

		db.Creations.Remove(creation);
		await db.SaveChangesAsync();

		// The row goes first: a folder left behind is only clutter, a row without its folder is a broken creation
		storage.DeleteFolder(creation.Id);
	}

	/// <summary>
	/// The creation with the program file read from its folder.
	/// </summary>
	/// <exception cref="NotFoundException">
	/// The creation has no program file on disk.
	/// </exception>
	private async Task<CreationDetailResponse> ToDetailResponse(Creation creation)
	{
		var programFile = await storage.ReadProgramFile(creation.Id)
			?? throw new NotFoundException($"The program file of creation {creation.Id} was not found on the server.");

		return new CreationDetailResponse(ToResponse(creation), programFile);
	}

	private static CreationResponse ToResponse(Creation creation)
	{
		return new CreationResponse(
			creation.Id,
			creation.UserId,
			creation.Title,
			creation.Description,
			creation.DurationSeconds,
			!string.IsNullOrWhiteSpace(creation.VideoFileName),
			creation.IsPublic,
			creation.CreatedAt,
			creation.UpdatedAt
		);
	}
}
