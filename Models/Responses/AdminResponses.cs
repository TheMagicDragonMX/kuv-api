namespace kuv_api.Models.Responses;

/// <summary>
/// A creation as the super admin panel lists it: what describes it, with its author, whether it is public or not.
/// </summary>
/// <param name="Id">
/// Identifier of the creation.
/// </param>
/// <param name="Title">
/// Title of the creation.
/// </param>
/// <param name="AuthorId">
/// Identifier of the user who made it.
/// </param>
/// <param name="AuthorUsername">
/// Username of the user who made it.
/// </param>
/// <param name="HasVideo">
/// Whether the creation has its preview (videos and thumbnail).
/// </param>
/// <param name="IsPublic">
/// Whether other users can see it.
/// </param>
/// <param name="CreatedAt">
/// When the creation was created.
/// </param>
/// <param name="UpdatedAt">
/// When the creation was last updated.
/// </param>
public sealed record AdminCreationResponse(
    int Id,
    string Title,
    int AuthorId,
    string AuthorUsername,
    bool HasVideo,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
