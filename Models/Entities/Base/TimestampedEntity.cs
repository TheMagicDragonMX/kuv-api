namespace kuv_api.Models.Entities.Base;

/// <summary>
/// Base class for entities that remember when they were created. The database context fills the value in.
/// </summary>
public abstract class TimestampedEntity
{
	/// <summary>
	/// When the entity was created.
	/// </summary>
	public DateTime CreatedAt { get; set; }
}
