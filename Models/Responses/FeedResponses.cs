namespace kuv_api.Models.Responses;

/// <summary>
/// A creation as the feed shows it: what a card needs, with its author.
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
/// <param name="AuthorPhoto">
/// Where the profile picture of the author can be fetched from (a path on this API), or null when they have none.
/// </param>
/// <param name="HasVideo">
/// Whether the creation has a video, which is served at <c>/creations/{id}/video</c>.
/// </param>
/// <param name="CreatedAt">
/// When the creation was created.
/// </param>
public sealed record FeedCreationResponse(
    int Id,
    string Title,
    int AuthorId,
    string AuthorUsername,
    string? AuthorPhoto,
    bool HasVideo,
    DateTime CreatedAt
);
