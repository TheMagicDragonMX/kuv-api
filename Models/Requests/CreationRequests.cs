using System.Text.Json.Nodes;

namespace kuv_api.Models.Requests;

/// <summary>
/// What describes a creation, without its program. This is what lists of creations return.
/// </summary>
public record CreationResponse(
	int Id,
	int UserId,
	string Title,
	string? Description,
	bool HasVideo,
	bool IsPublic,
	DateTime CreatedAt,
	DateTime UpdatedAt
);

/// <summary>
/// A creation with its program file, as returned when one creation is asked for, created or updated.
/// </summary>
public sealed record CreationDetailResponse : CreationResponse
{
	/// <summary>
	/// Builds the answer from the description of the creation and its program file.
	/// </summary>
	/// <param name="creation">
	/// What describes the creation.
	/// </param>
	/// <param name="programFile">
	/// The content of its <c>program.json</c>.
	/// </param>
	public CreationDetailResponse(CreationResponse creation, JsonNode programFile) : base(creation)
	{
		ProgramFile = programFile;
	}

	/// <summary>
	/// The program file: its <c>version</c>, the <c>program</c> and the <c>environment</c>, as defined by
	/// <c>Schemas/program-file.schema.json</c>.
	/// </summary>
	public JsonNode ProgramFile { get; init; }
}

/// <summary>
/// Request to change visibility of a creation.
/// </summary>
public sealed record SetVisibilityRequest(bool IsPublic);

/// <summary>
/// Data for creating a new cube creation.
/// </summary>
public sealed class CreateCreationRequest
{
	/// <summary>
	/// Title of the creation.
	/// </summary>
	public required string Title { get; set; }

	/// <summary>
	/// Optional description.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// The program file, as JSON text that follows <c>Schemas/program-file.schema.json</c>.
	/// </summary>
	public required string ProgramFile { get; set; }

	/// <summary>
	/// Whether it's public. Defaults to false.
	/// </summary>
	public bool IsPublic { get; set; } = false;

	/// <summary>
	/// Optional recorded video file (e.g. webm).
	/// </summary>
	public IFormFile? Video { get; set; }
}

/// <summary>
/// Data for updating an existing cube creation.
/// </summary>
public sealed class UpdateCreationRequest
{
	/// <summary>
	/// Title of the creation. Left as it was when empty.
	/// </summary>
	public string? Title { get; set; }

	/// <summary>
	/// Optional description.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// The new program file, as JSON text that follows <c>Schemas/program-file.schema.json</c>. Left as it was when
	/// missing.
	/// </summary>
	public string? ProgramFile { get; set; }

	/// <summary>
	/// Whether it's public.
	/// </summary>
	public bool? IsPublic { get; set; }

	/// <summary>
	/// Optional replacement video file.
	/// </summary>
	public IFormFile? Video { get; set; }
}
