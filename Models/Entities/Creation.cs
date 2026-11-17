using System.ComponentModel.DataAnnotations.Schema;
using kuv_api.Models.Entities.Base;

namespace kuv_api.Models.Entities;

/// <summary>
/// A user-created animation or project for the cube. The database only describes it (who made it, its title, when it
/// changed...); its program is the <c>program.json</c> file in its own folder, <c>uploads/creations/{id}</c>, next to its video.
/// </summary>
public class Creation : BaseEntity
{
	/// <summary>
	/// The user who owns this creation.
	/// </summary>
	public required int UserId { get; set; }

	/// <summary>
	/// Navigation property to the owner user.
	/// </summary>
	public User User { get; set; } = null!;

	/// <summary>
	/// Title of the creation.
	/// </summary>
	public required string Title { get; set; }

	/// <summary>
	/// Optional description of the creation.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Duration of the recorded animation in seconds.
	/// </summary>
	public double DurationSeconds { get; set; }

	/// <summary>
	/// Name of the video file inside the folder of the creation (<c>uploads/creations/{id}</c>), or null when it has no video.
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
	/// The program of the creation as it was stored before it moved to <c>program.json</c>. Only the startup import
	/// reads it; it is null for every creation that has been imported or created since.
	/// </summary>
	[Column("ProgramJson")]
	public string? LegacyProgramJson { get; set; }

	/// <summary>
	/// The environment of the creation as it was stored before it moved to <c>program.json</c>. Only the startup
	/// import reads it; it is null for every creation that has been imported or created since.
	/// </summary>
	[Column("EnvironmentJson")]
	public string? LegacyEnvironmentJson { get; set; }
}

