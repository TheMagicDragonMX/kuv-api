using kuv_api.Data;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Entities;
using kuv_api.Models.Requests;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Handles profiles and who follows whom. Every method takes the id of the signed-in user
/// (the viewer), because what a profile says (whether they follow each other) depends on who looks.
/// The profile pictures are files, kept by <see cref="ProfilePhotoService"/>.
/// </summary>
public sealed class ProfileService(KuvDbContext dbContext, ProfilePhotoService photos)
{
    /// <summary>
    /// Longest bio a profile can have, in characters.
    /// </summary>
    public const int MaxBioLength = 280;

    /// <summary>
    /// Most users a search answers with.
    /// </summary>
    private const int SearchLimit = 20;

    private readonly KuvDbContext dbContext = dbContext;

    private readonly ProfilePhotoService photos = photos;

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
                user.Bio,
                null,
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
                user.Id == viewerId,
                user.Id == viewerId && user.IsSuperAdmin
            ))
            .SingleOrDefaultAsync()
            ?? throw new NotFoundException("Ese usuario no existe.");

        // The picture is a file, so it cannot be part of the query
        return profile with { Photo = await photos.UrlFor(profile.Id) };
    }

    /// <summary>
    /// Changes the bio of the viewer.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="request">
    /// The new data; a blank bio is cleared.
    /// </param>
    /// <returns>
    /// The profile after the change.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The bio is too long.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// The viewer no longer exists.
    /// </exception>
    public async Task<ProfileResponse> UpdateProfile(int viewerId, UpdateProfileRequest request)
    {
        var bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();

        if (bio is not null && bio.Length > MaxBioLength)
        {
            throw new BadRequestException($"La biografía puede tener hasta {MaxBioLength} caracteres.");
        }

        var viewer = await dbContext.Users.SingleOrDefaultAsync(user => user.Id == viewerId)
            ?? throw new NotFoundException("Ese usuario no existe.");

        viewer.Bio = bio;
        await dbContext.SaveChangesAsync();

        return await GetProfile(viewerId, viewerId);
    }

    /// <summary>
    /// Sets the profile picture of the viewer, replacing the one they had.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="request">
    /// The image.
    /// </param>
    /// <returns>
    /// The profile after the change.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The file is not a PNG, JPEG, WebP or GIF of up to 300 KB.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// The viewer no longer exists.
    /// </exception>
    public async Task<ProfileResponse> SetPhoto(int viewerId, UpdatePhotoRequest request)
    {
        await EnsureExists(viewerId);
        await photos.Save(viewerId, request.Photo);

        return await GetProfile(viewerId, viewerId);
    }

    /// <summary>
    /// Removes the profile picture of the viewer.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <returns>
    /// The profile after the change.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// The viewer no longer exists.
    /// </exception>
    public async Task<ProfileResponse> RemovePhoto(int viewerId)
    {
        await EnsureExists(viewerId);
        await photos.Delete(viewerId);

        return await GetProfile(viewerId, viewerId);
    }

    /// <summary>
    /// The profile picture of a user.
    /// </summary>
    /// <param name="userId">
    /// The user whose picture is wanted.
    /// </param>
    /// <returns>
    /// The file, with its type and the moment it was saved.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// The user has no picture.
    /// </exception>
    public async Task<StoredPhoto> GetPhoto(int userId)
    {
        return await photos.Find(userId);
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
            .Select(follow => follow.Follower);

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
            .Select(follow => follow.Followed);

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
            dbContext.Follows.Add(new Follow { FollowerId = viewerId, FollowedId = userId });
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
    private async Task<List<UserSummaryResponse>> Summaries(int viewerId, IQueryable<User> users)
    {
        var summaries = await users
            .Select(user => new UserSummaryResponse(
                user.Id,
                user.Username,
                user.Bio,
                null,
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

        // The pictures are files, so they cannot be part of the query
        var urls = await Task.WhenAll(summaries.Select(summary => photos.UrlFor(summary.Id)));

        return summaries.Select((summary, index) => summary with { Photo = urls[index] }).ToList();
    }
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
/// <param name="Bio">
/// What the user tells about themselves, if anything.
/// </param>
/// <param name="Photo">
/// Where the profile picture can be fetched from (a path on this API, such as <c>/users/7/photo?v=...</c>), or null
/// when the user has none.
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
/// <param name="IsSuperAdmin">
/// Whether the viewer is a super admin; only true on the viewer's own profile, so nobody learns who the others are.
/// </param>
public sealed record ProfileResponse(
    int Id,
    string Username,
    string? Email,
    string? Bio,
    string? Photo,
    int Followers,
    int Following,
    bool FollowsYou,
    bool YouFollow,
    bool IsYou,
    bool IsSuperAdmin
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
/// <param name="Bio">
/// What the user tells about themselves, if anything.
/// </param>
/// <param name="Photo">
/// Where the profile picture can be fetched from (a path on this API, such as <c>/users/7/photo?v=...</c>), or null
/// when the user has none.
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
    string? Bio,
    string? Photo,
    int Followers,
    bool FollowsYou,
    bool YouFollow
);
