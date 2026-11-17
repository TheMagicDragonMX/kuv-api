namespace kuv_api.Models.Entities.Base;

/// <summary>
/// An entity that remembers when it was created. <c>KuvDbContext</c> fills the value in when the entity is inserted.
/// </summary>
public interface ICreatedAt
{
	/// <summary>
	/// When the entity was created.
	/// </summary>
	DateTime CreatedAt { get; set; }
}
