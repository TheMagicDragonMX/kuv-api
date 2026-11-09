namespace kuv_api.Models;

/// <summary>
/// A user-created animation or project for the cube.
/// </summary>
public class Creation : BaseEntity
{
	/// <summary>
	/// The user who owns this creation.
	/// </summary>
	public required int UserId { get; set; }

	/// <summary>
	/// Title of the creation.
	/// </summary>
	public required string Title { get; set; }

	/// <summary>
	/// Optional description of the creation.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Serialized JSON string of the cube program.
	/// </summary>
	public required string ProgramJson { get; set; }

	/// <summary>
	/// Serialized JSON string of the cube environment (lighting, background).
	/// </summary>
	public string? EnvironmentJson { get; set; }

	/// <summary>
	/// Duration of the recorded animation in seconds.
	/// </summary>
	public double DurationSeconds { get; set; }

	/// <summary>
	/// Relative or unique file name of the video saved on the API file system.
	/// </summary>
	public string? VideoFileName { get; set; }

	/// <summary>
	/// Content type of the video file (e.g. video/webm).
	/// </summary>
	public string? VideoMimeType { get; set; }

	/// <summary>
	/// Whether this creation is public or private. Defaults to false (private).
	/// </summary>
	public bool IsPublic { get; set; } = false;

	/// <summary>
	/// When the creation was created.
	/// </summary>
	public required DateTime CreatedAt { get; set; }

	/// <summary>
	/// When the creation was last updated.
	/// </summary>
	public required DateTime UpdatedAt { get; set; }

	/// <summary>
	/// Navigation property to the owner user.
	/// </summary>
	public User? User { get; set; }
}

