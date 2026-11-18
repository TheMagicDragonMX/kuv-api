using kuv_api.Models.Entities.Base;

namespace kuv_api.Models.Entities;

/// <summary>
/// A user-created animation or project for the cube. The database only describes it (who made it, its title, when it
/// changed...); its program is the <c>program.json</c> file in its own folder, <c>creations/{id}</c> of the file store, next to its video.
/// </summary>
public class Creation : BaseEntity, ICreatedAt, IUpdatedAt
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
	/// Whether the creation has a generated video, the <c>preview</c> file in its folder (<c>creations/{id}</c> of the file store).
	/// </summary>
	public bool HasVideo { get; set; }

	/// <summary>
	/// Whether this creation is public or private. Defaults to false (private).
	/// </summary>
	public bool IsPublic { get; set; } = false;

	/// <summary>
	/// When the creation was created.
	/// </summary>
	public DateTime CreatedAt { get; set; }

	/// <summary>
	/// When the creation was last updated.
	/// </summary>
	public DateTime UpdatedAt { get; set; }
}
