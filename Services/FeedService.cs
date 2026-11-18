using kuv_api.Data;
using kuv_api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Builds the categories of the feed (the home page of the web app). Each category is a method that answers the
/// public creations that belong in it. Only public creations are ever shown.
/// </summary>
public sealed class FeedService(KuvDbContext dbContext, ProfilePhotoService photos)
{
    /// <summary>
    /// Most creations a category answers when the caller does not say.
    /// </summary>
    public const int DefaultLimit = 20;

    /// <summary>
    /// Most creations a category answers, whatever the caller asks for.
    /// </summary>
    public const int MaxLimit = 50;

    private readonly KuvDbContext dbContext = dbContext;

    private readonly ProfilePhotoService photos = photos;

    /// <summary>
    /// The newest public creations, the most recent first.
    /// </summary>
    /// <param name="limit">
    /// How many to answer at most. It is kept between 1 and <see cref="MaxLimit"/>.
    /// </param>
    /// <returns>
    /// The creations, newest first.
    /// </returns>
    public async Task<List<FeedCreationResponse>> NewCreations(int limit)
    {
        var count = Math.Clamp(limit, 1, MaxLimit);

        var creations = await dbContext.Creations
            .Where(creation => creation.IsPublic)
            .OrderByDescending(creation => creation.CreatedAt)
            .ThenByDescending(creation => creation.Id)
            .Take(count)
            .Select(creation => new
            {
                creation.Id,
                creation.Title,
                creation.UserId,
                creation.User.Username,
                creation.HasVideo,
                creation.CreatedAt
            })
            .ToListAsync();

        // One lookup per author, not per creation: an author can have several in the list
        var authors = creations.Select(creation => creation.UserId).Distinct().ToList();
        var urls = new Dictionary<int, string?>();

        foreach (var authorId in authors)
        {
            urls[authorId] = await photos.UrlFor(authorId);
        }

        return creations
            .Select(creation => new FeedCreationResponse(
                creation.Id,
                creation.Title,
                creation.UserId,
                creation.Username,
                urls[creation.UserId],
                creation.HasVideo,
                creation.CreatedAt
            ))
            .ToList();
    }
}
