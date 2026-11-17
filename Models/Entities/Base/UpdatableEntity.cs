namespace kuv_api.Models.Entities.Base;

/// <summary>
/// Base class for entities that remember when they were created and last updated. The database context fills the values in.
/// </summary>
public abstract class UpdatableEntity : TimestampedEntity
{
	/// <summary>
	/// When the entity was last updated.
	/// </summary>
	public DateTime UpdatedAt { get; set; }
}
