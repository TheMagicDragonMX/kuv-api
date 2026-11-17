namespace kuv_api.Models.Entities.Base;

/// <summary>
/// Base class for the database entities that have an id key, with their creation and update times.
/// </summary>
public abstract class BaseEntity : UpdatableEntity
{
    /// <summary>
    /// Primary key of the entity.
    /// </summary>
    public int Id { get; set; }
}