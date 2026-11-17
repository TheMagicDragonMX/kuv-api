namespace kuv_api.Models.Entities.Base;

/// <summary>
/// An entity that remembers when it was last updated. <c>KuvDbContext</c> fills the value in when the entity is
/// inserted or changed.
/// </summary>
public interface IUpdatedAt
{
	/// <summary>
	/// When the entity was last updated.
	/// </summary>
	DateTime UpdatedAt { get; set; }
}
