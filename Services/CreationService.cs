using kuv_api.Data;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Files;
using kuv_api.Models.Entities;
using kuv_api.Models.Requests;
using kuv_api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Manages user creations: what describes them is in the database, and each one has its own folder on disk with its
/// program file, its videos and its thumbnail (see <see cref="CreationStorage"/>).
/// </summary>
public sealed class CreationService(KuvDbContext db, CreationStorage storage, ProgramFileValidator validator)
{
	private readonly KuvDbContext db = db;

	/// <summary>
	/// Opens the video of a creation for a theme, with its content type. Whoever receives the video must dispose its
	/// stream.
	/// </summary>
	/// <exception cref="BadRequestException">
	/// The theme is not one of <see cref="CreationStorage.VideoThemes"/>.
	/// </exception>
	public async Task<(StoredFile Video, string ContentType)> GetCreationVideo(int viewerId, int creationId, string theme)
	{
		if (!CreationStorage.VideoThemes.Contains(theme))
		{
			throw new BadRequestException("El tema del video debe ser dark o light.");
		}

		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		await EnsureCanSee(creation, viewerId);

		if (!creation.HasVideo)
		{
			throw new NotFoundException("This creation does not have a video file.");
		}

		return await storage.OpenVideo(creation.Id, theme)
			?? throw new NotFoundException("The video file was not found on the server.");
	}

	/// <summary>
	/// Opens the thumbnail of a creation, a 64x64 PNG. Whoever receives it must dispose its stream.
	/// </summary>
	/// <exception cref="NotFoundException">
	/// The creation does not exist, or has no thumbnail (it has no video, or its video was made before there were
	/// thumbnails).
	/// </exception>
	public async Task<StoredFile> GetCreationThumbnail(int viewerId, int creationId)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		await EnsureCanSee(creation, viewerId);

		if (!creation.HasVideo)
		{
			throw new NotFoundException("This creation does not have a thumbnail.");
		}

		return await storage.OpenThumbnail(creation.Id)
			?? throw new NotFoundException("The thumbnail was not found on the server.");
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
	/// Returns a specific creation by ID if owned by the viewer, if public, or if the viewer is a super admin.
	/// </summary>
	public async Task<CreationDetailResponse> GetCreation(int viewerId, int creationId)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		await EnsureCanSee(creation, viewerId);

		return await ToDetailResponse(creation);
	}

	/// <summary>
	/// Creates a new creation: a row in the database, a folder with its program file and, when given, its preview
	/// (videos and thumbnail).
	/// </summary>
	public async Task<CreationDetailResponse> Create(int userId, CreateCreationRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Title))
		{
			throw new BadRequestException("Title is required.");
		}

		// Checked before anything is stored, so a bad program leaves no row and no folder behind
		var programFile = validator.Parse(request.ProgramFile);
		var preview = PreviewOf(request.VideoDark, request.VideoLight, request.Thumbnail);

		var creation = new Creation
		{
			UserId = userId,
			Title = request.Title.Trim(),
			Description = request.Description?.Trim(),
			IsPublic = request.IsPublic,
		};

		db.Creations.Add(creation);
		await db.SaveChangesAsync();

		try
		{
			await storage.WriteProgramFile(creation.Id, programFile);

			if (preview is var (dark, light, thumbnail))
			{
				await storage.WriteVideos(creation.Id, dark, light);
				await storage.WriteThumbnail(creation.Id, thumbnail);
				creation.HasVideo = true;
				await db.SaveChangesAsync();
			}
		}

		catch
		{
			// A creation without its files would be unusable, so it is taken back
			await storage.DeleteFolder(creation.Id);
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
		var preview = PreviewOf(request.VideoDark, request.VideoLight, request.Thumbnail);

		if (!string.IsNullOrWhiteSpace(request.Title))
		{
			creation.Title = request.Title.Trim();
		}

		creation.Description = request.Description?.Trim();

		if (request.IsPublic.HasValue)
		{
			creation.IsPublic = request.IsPublic.Value;
		}

		if (programFile != null)
		{
			await storage.WriteProgramFile(creation.Id, programFile);
		}

		if (preview is var (dark, light, thumbnail))
		{
			await storage.WriteVideos(creation.Id, dark, light);
			await storage.WriteThumbnail(creation.Id, thumbnail);
			creation.HasVideo = true;
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
		await storage.DeleteFolder(creation.Id);
	}

	/// <summary>
	/// Every creation of every user, public or not, the most recent first, with its author. Only for super admins:
	/// whoever calls it checks that first (see <see cref="AdminService"/>).
	/// </summary>
	public async Task<List<AdminCreationResponse>> GetAllCreations()
	{
		return await db.Creations
			.OrderByDescending(creation => creation.CreatedAt)
			.ThenByDescending(creation => creation.Id)
			.Select(creation => new AdminCreationResponse(
				creation.Id,
				creation.Title,
				creation.UserId,
				creation.User.Username,
				creation.HasVideo,
				creation.IsPublic,
				creation.CreatedAt,
				creation.UpdatedAt
			))
			.ToListAsync();
	}

	/// <summary>
	/// Replaces the preview of a creation (its two videos and its thumbnail) and nothing else, whoever owns it. Only
	/// for super admins: whoever calls it checks that first (see <see cref="AdminService"/>).
	/// </summary>
	/// <exception cref="NotFoundException">
	/// The creation does not exist.
	/// </exception>
	/// <exception cref="BadRequestException">
	/// A file is missing or the thumbnail is not a small PNG.
	/// </exception>
	public async Task<CreationResponse> SetPreview(int creationId, SetPreviewRequest request)
	{
		var creation = await db.Creations.FindAsync(creationId)
			?? throw new NotFoundException($"Creation {creationId} was not found.");

		var (dark, light, thumbnail) = PreviewOf(request.VideoDark, request.VideoLight, request.Thumbnail)
			?? throw new BadRequestException("Se necesitan los dos videos, el del tema oscuro y el del claro, y la miniatura.");

		await storage.WriteVideos(creation.Id, dark, light);
		await storage.WriteThumbnail(creation.Id, thumbnail);
		creation.HasVideo = true;
		await db.SaveChangesAsync();

		return ToResponse(creation);
	}

	/// <summary>
	/// Lets the viewer see a creation when it is theirs, when it is public, or when they are a super admin.
	/// </summary>
	/// <exception cref="ForbiddenException">
	/// It is someone else's private creation and the viewer is not a super admin.
	/// </exception>
	private async Task EnsureCanSee(Creation creation, int viewerId)
	{
		if (creation.UserId == viewerId || creation.IsPublic)
		{
			return;
		}

		if (!await db.Users.AnyAsync(user => user.Id == viewerId && user.IsSuperAdmin))
		{
			throw new ForbiddenException("You cannot access this creation.");
		}
	}

	/// <summary>
	/// The preview of a request (its two videos, one per theme, and its thumbnail), or null when it brings none.
	/// </summary>
	/// <exception cref="BadRequestException">
	/// Only part of it came (a creation has the three files or none), or the thumbnail is not a small PNG.
	/// </exception>
	private static (IFormFile Dark, IFormFile Light, IFormFile Thumbnail)? PreviewOf(IFormFile? dark, IFormFile? light, IFormFile? thumbnail)
	{
		var hasDark = dark is not null && 0 < dark.Length;
		var hasLight = light is not null && 0 < light.Length;
		var hasThumbnail = thumbnail is not null && 0 < thumbnail.Length;

		if (hasDark != hasLight || hasDark != hasThumbnail)
		{
			throw new BadRequestException("Se necesitan los dos videos, el del tema oscuro y el del claro, y la miniatura.");
		}

		if (!hasDark)
		{
			return null;
		}

		if (MaxThumbnailBytes < thumbnail!.Length || !IsPng(thumbnail))
		{
			throw new BadRequestException("La miniatura debe ser una imagen PNG de menos de 64 KB.");
		}

		return (dark!, light!, thumbnail);
	}

	/// <summary>
	/// The largest thumbnail accepted, in bytes. A 64x64 PNG takes far less, even with every pixel different.
	/// </summary>
	private const long MaxThumbnailBytes = 64 * 1024;

	/// <summary>
	/// The first bytes of every PNG file.
	/// </summary>
	private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

	/// <summary>
	/// Whether an uploaded file starts like a PNG, whatever its name or content type say.
	/// </summary>
	private static bool IsPng(IFormFile file)
	{
		using var content = file.OpenReadStream();
		var header = new byte[PngSignature.Length];

		return header.Length == content.ReadAtLeast(header, header.Length, throwOnEndOfStream: false)
			&& header.SequenceEqual(PngSignature);
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
			creation.HasVideo,
			creation.IsPublic,
			creation.CreatedAt,
			creation.UpdatedAt
		);
	}
}
