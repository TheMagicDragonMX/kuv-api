namespace kuv_api.Models.Entities.Base;

/// <summary>
/// Base class for the database entities that have an id key.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Primary key of the entity.
    /// </summary>
    public int Id { get; set; }
}