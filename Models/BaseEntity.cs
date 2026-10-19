namespace kuv_api.Models;

/// <summary>
/// Base class for all database entities.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Primary key of the entity.
    /// </summary>
    public int Id { get; set; }
}