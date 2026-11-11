namespace kuv_api.Requests;

/// <summary>
/// A creation returned to the client.
/// </summary>
public sealed record CreationResponse(
	int Id,
	int UserId,
	string Title,
	string? Description,
	string ProgramJson,
	string? EnvironmentJson,
	double DurationSeconds,
	bool HasVideo,
	bool IsPublic,
	DateTime CreatedAt,
	DateTime UpdatedAt
);

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
	/// Cube program JSON.
	/// </summary>
	public required string ProgramJson { get; set; }

	/// <summary>
	/// Optional cube environment JSON.
	/// </summary>
	public string? EnvironmentJson { get; set; }

	/// <summary>
	/// Animation duration in seconds.
	/// </summary>
	public double DurationSeconds { get; set; }

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
	/// Title of the creation.
	/// </summary>
	public required string Title { get; set; }

	/// <summary>
	/// Optional description.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Cube program JSON.
	/// </summary>
	public required string ProgramJson { get; set; }

	/// <summary>
	/// Optional cube environment JSON.
	/// </summary>
	public string? EnvironmentJson { get; set; }

	/// <summary>
	/// Animation duration in seconds.
	/// </summary>
	public double DurationSeconds { get; set; }

	/// <summary>
	/// Whether it's public.
	/// </summary>
	public bool? IsPublic { get; set; }

	/// <summary>
	/// Optional replacement video file.
	/// </summary>
	public IFormFile? Video { get; set; }
}
