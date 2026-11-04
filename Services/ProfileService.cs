using kuv_api.Data;
using kuv_api.Exceptions;
using kuv_api.Models;
using kuv_api.Requests;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Handles profiles and who follows whom. Every method takes the id of the signed-in user
/// (the viewer), because what a profile says (whether they follow each other) depends on who looks.
/// </summary>
public sealed class ProfileService(KuvDbContext dbContext)
{
    /// <summary>
    /// Longest description a profile can have, in characters.
    /// </summary>
    public const int MaxDescriptionLength = 280;

    /// <summary>
    /// Longest profile picture, in characters of its data URL (about 300 KB of image).
    /// </summary>
    public const int MaxPhotoLength = 400_000;

    /// <summary>
    /// Most users a search answers with.
    /// </summary>
    private const int SearchLimit = 20;

    /// <summary>
    /// Start of the data URLs accepted as a profile picture.
    /// </summary>
    private static readonly string[] PhotoPrefixes = ["data:image/png;base64,", "data:image/jpeg;base64,", "data:image/webp;base64,", "data:image/gif;base64,"];

    private readonly KuvDbContext dbContext = dbContext;

    /// <summary>
    /// The profile of a user as the viewer sees it.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="userId">
    /// The user whose profile is read.
    /// </param>
    /// <returns>
    /// The profile; the email is only included when it is the viewer's own.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    public async Task<ProfileResponse> GetProfile(int viewerId, int userId)
    {
        var profile = await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => new ProfileResponse(
                user.Id,
                user.Username,
                user.Id == viewerId ? user.Email : null,
                user.Description,
                user.Photo,
                dbContext.Follows.Count(follow => follow.FollowedId == user.Id),
                dbContext.Follows.Count(follow => follow.FollowerId == user.Id),
                dbContext.Follows.Any(follow =>
                    follow.FollowerId == user.Id &&
                    follow.FollowedId == viewerId
                ),
                dbContext.Follows.Any(follow =>
                    follow.FollowerId == viewerId &&
                    follow.FollowedId == user.Id
                ),
                user.Id == viewerId
            ))
            .SingleOrDefaultAsync();

        return profile ?? throw new NotFoundException("Ese usuario no existe.");
    }

    /// <summary>
    /// Changes the description and picture of the viewer.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="request">
    /// The new data; a blank field is cleared.
    /// </param>
    /// <returns>
    /// The profile after the change.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The description is too long, or the picture is not an image data URL or is too big.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// The viewer no longer exists.
    /// </exception>
    public async Task<ProfileResponse> UpdateProfile(int viewerId, UpdateProfileRequest request)
    {
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var photo = string.IsNullOrWhiteSpace(request.Photo) ? null : request.Photo;

        if (description is not null && description.Length > MaxDescriptionLength)
        {
            throw new BadRequestException($"La descripción puede tener hasta {MaxDescriptionLength} caracteres.");
        }

        if (photo is not null && (photo.Length > MaxPhotoLength || !PhotoPrefixes.Any(prefix => photo.StartsWith(prefix, StringComparison.Ordinal))))
        {
            throw new BadRequestException("La foto debe ser una imagen PNG, JPEG, WebP o GIF de hasta 300 KB.");
        }

        var viewer = await dbContext.Users.SingleOrDefaultAsync(user => user.Id == viewerId)
            ?? throw new NotFoundException("Ese usuario no existe.");

        viewer.Description = description;
        viewer.Photo = photo;
        await dbContext.SaveChangesAsync();

        return await GetProfile(viewerId, viewerId);
    }

    /// <summary>
    /// The users who follow a user, the most recent first.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="userId">
    /// The user whose followers are listed.
    /// </param>
    /// <returns>
    /// The followers, as the viewer sees them.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    public async Task<List<UserSummaryResponse>> Followers(int viewerId, int userId)
    {
        await EnsureExists(userId);

        var followers = dbContext.Follows
            .Where(follow => follow.FollowedId == userId)
            .OrderByDescending(follow => follow.CreatedAt)
            .Select(follow => follow.Follower!);

        return await Summaries(viewerId, followers);
    }

    /// <summary>
    /// The users a user follows, the most recent first.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="userId">
    /// The user whose follows are listed.
    /// </param>
    /// <returns>
    /// The followed users, as the viewer sees them.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    public async Task<List<UserSummaryResponse>> Following(int viewerId, int userId)
    {
        await EnsureExists(userId);

        var following = dbContext.Follows
            .Where(follow => follow.FollowerId == userId)
            .OrderByDescending(follow => follow.CreatedAt)
            .Select(follow => follow.Followed!);

        return await Summaries(viewerId, following);
    }

    /// <summary>
    /// Users whose name contains the text, leaving the viewer out.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="text">
    /// Part of the username; a blank text finds nobody.
    /// </param>
    /// <returns>
    /// Up to 20 users, by name.
    /// </returns>
    public async Task<List<UserSummaryResponse>> Search(int viewerId, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var pattern = $"%{text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
        var users = dbContext.Users
            .Where(user =>
                user.Id != viewerId &&
                EF.Functions.ILike(user.Username, pattern)
            )
            .OrderBy(user => user.Username)
            .Take(SearchLimit);

        return await Summaries(viewerId, users);
    }

    /// <summary>
    /// Makes the viewer follow a user. Following someone already followed changes nothing.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="userId">
    /// The user to follow.
    /// </param>
    /// <returns>
    /// The profile of the followed user.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The viewer tried to follow themselves.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    public async Task<ProfileResponse> Follow(int viewerId, int userId)
    {
        if (viewerId == userId)
        {
            throw new BadRequestException("No puedes seguirte a ti mismo.");
        }

        await EnsureExists(userId);

        var alreadyFollows = await dbContext.Follows.AnyAsync(follow =>
            follow.FollowerId == viewerId &&
            follow.FollowedId == userId
        );

        if (!alreadyFollows)
        {
            dbContext.Follows.Add(new Follow { FollowerId = viewerId, FollowedId = userId, CreatedAt = DateTime.UtcNow });
            await dbContext.SaveChangesAsync();
        }

        return await GetProfile(viewerId, userId);
    }

    /// <summary>
    /// Makes the viewer stop following a user. Unfollowing someone not followed changes nothing.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="userId">
    /// The user to stop following.
    /// </param>
    /// <returns>
    /// The profile of that user.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    public async Task<ProfileResponse> Unfollow(int viewerId, int userId)
    {
        await EnsureExists(userId);
        await dbContext.Follows
            .Where(follow =>
                follow.FollowerId == viewerId &&
                follow.FollowedId == userId
            )
            .ExecuteDeleteAsync();

        return await GetProfile(viewerId, userId);
    }

    /// <summary>
    /// Throws when there is no user with the id.
    /// </summary>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    private async Task EnsureExists(int userId)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Id == userId))
        {
            throw new NotFoundException("Ese usuario no existe.");
        }
    }

    /// <summary>
    /// The users of a query as list entries, as the viewer sees them.
    /// </summary>
    private async Task<List<UserSummaryResponse>> Summaries(int viewerId, IQueryable<User> users) =>
        await users
            .Select(user => new UserSummaryResponse(
                user.Id,
                user.Username,
                user.Description,
                user.Photo,
                dbContext.Follows.Count(follow => follow.FollowedId == user.Id),
                dbContext.Follows.Any(follow =>
                    follow.FollowerId == user.Id &&
                    follow.FollowedId == viewerId
                ),
                dbContext.Follows.Any(follow =>
                    follow.FollowerId == viewerId &&
                    follow.FollowedId == user.Id
                )
            ))
            .ToListAsync();
}

/// <summary>
/// A user's profile as the viewer sees it.
/// </summary>
/// <param name="Id">
/// Identifier of the user.
/// </param>
/// <param name="Username">
/// Username of the user.
/// </param>
/// <param name="Email">
/// Email of the user; only present on the viewer's own profile.
/// </param>
/// <param name="Description">
/// What the user tells about themselves, if anything.
/// </param>
/// <param name="Photo">
/// Profile picture as a data URL, if any.
/// </param>
/// <param name="Followers">
/// How many users follow them.
/// </param>
/// <param name="Following">
/// How many users they follow.
/// </param>
/// <param name="FollowsYou">
/// Whether they follow the viewer.
/// </param>
/// <param name="YouFollow">
/// Whether the viewer follows them.
/// </param>
/// <param name="IsYou">
/// Whether it is the viewer's own profile.
/// </param>
public sealed record ProfileResponse(
    int Id,
    string Username,
    string? Email,
    string? Description,
    string? Photo,
    int Followers,
    int Following,
    bool FollowsYou,
    bool YouFollow,
    bool IsYou
);

/// <summary>
/// A user in a list (followers, followed, search results), as the viewer sees them.
/// </summary>
/// <param name="Id">
/// Identifier of the user.
/// </param>
/// <param name="Username">
/// Username of the user.
/// </param>
/// <param name="Description">
/// What the user tells about themselves, if anything.
/// </param>
/// <param name="Photo">
/// Profile picture as a data URL, if any.
/// </param>
/// <param name="Followers">
/// How many users follow them.
/// </param>
/// <param name="FollowsYou">
/// Whether they follow the viewer.
/// </param>
/// <param name="YouFollow">
/// Whether the viewer follows them.
/// </param>
public sealed record UserSummaryResponse(
    int Id,
    string Username,
    string? Description,
    string? Photo,
    int Followers,
    bool FollowsYou,
    bool YouFollow
);
