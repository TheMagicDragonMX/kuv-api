using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Requests;
using kuv_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kuv_api.Controllers;

/// <summary>
/// Profiles and follows. Every endpoint needs the access token of a signed-in user, except the one that serves a profile picture.
/// </summary>
[ApiController]
[Authorize]
[Route("users")]
public class UsersController(ProfileService profileService) : ControllerBase
{
    private readonly ProfileService profileService = profileService;

    /// <summary>
    /// The profile of the signed-in user, with their email.
    /// </summary>
    /// <returns>
    /// 200 OK with the profile.
    /// </returns>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// The user no longer exists.
    /// </exception>
    [HttpGet("me")]
    public async Task<ActionResult<ProfileResponse>> Me() =>
        Ok(await profileService.GetProfile(ViewerId(), ViewerId()));

    /// <summary>
    /// Changes the bio of the signed-in user.
    /// </summary>
    /// <param name="request">
    /// The new data; a blank bio is cleared.
    /// </param>
    /// <returns>
    /// 200 OK with the profile after the change.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The bio is too long.
    /// </exception>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateMe(UpdateProfileRequest request) =>
        Ok(await profileService.UpdateProfile(ViewerId(), request));

    /// <summary>
    /// Sets the profile picture of the signed-in user, replacing the one they had.
    /// </summary>
    /// <param name="request">
    /// The image, as a multipart form with a <c>photo</c> file.
    /// </param>
    /// <returns>
    /// 200 OK with the profile after the change.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The file is not a PNG, JPEG, WebP or GIF of up to 300 KB.
    /// </exception>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpPut("me/photo")]
    public async Task<ActionResult<ProfileResponse>> SetMyPhoto([FromForm] UpdatePhotoRequest request) =>
        Ok(await profileService.SetPhoto(ViewerId(), request));

    /// <summary>
    /// Removes the profile picture of the signed-in user.
    /// </summary>
    /// <returns>
    /// 200 OK with the profile after the change.
    /// </returns>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpDelete("me/photo")]
    public async Task<ActionResult<ProfileResponse>> RemoveMyPhoto() =>
        Ok(await profileService.RemovePhoto(ViewerId()));

    /// <summary>
    /// The profile picture of a user. It needs no token, so a plain image element can load it; the address the
    /// profiles give ends in <c>?v=</c> and changes with the picture, so the answer can be cached for a year.
    /// </summary>
    /// <param name="id">
    /// Identifier of the user.
    /// </param>
    /// <param name="v">
    /// Version of the picture, from the address a profile gives. Only used to tell the cache the picture changed.
    /// </param>
    /// <returns>
    /// 200 OK with the image.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// The user has no picture.
    /// </exception>
    [AllowAnonymous]
    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> Photo(int id, [FromQuery] long? v)
    {
        var photo = await profileService.GetPhoto(id);

        // The type is decided by the server from the content, so the browser must not guess another
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = v is null ? "no-cache" : "public, max-age=31536000, immutable";

        return File(photo.Content, photo.ContentType, photo.LastModified, entityTag: null);
    }

    /// <summary>
    /// Finds users by part of their name.
    /// </summary>
    /// <param name="search">
    /// Part of the username.
    /// </param>
    /// <returns>
    /// 200 OK with up to 20 users.
    /// </returns>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpGet]
    public async Task<ActionResult<List<UserSummaryResponse>>> Search([FromQuery] string? search) =>
        Ok(await profileService.Search(ViewerId(), search));

    /// <summary>
    /// The profile of a user, without their email.
    /// </summary>
    /// <param name="id">
    /// Identifier of the user.
    /// </param>
    /// <returns>
    /// 200 OK with the profile.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProfileResponse>> Profile(int id) =>
        Ok(await profileService.GetProfile(ViewerId(), id));

    /// <summary>
    /// The users who follow a user.
    /// </summary>
    /// <param name="id">
    /// Identifier of the user.
    /// </param>
    /// <returns>
    /// 200 OK with the followers, the most recent first.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpGet("{id:int}/followers")]
    public async Task<ActionResult<List<UserSummaryResponse>>> Followers(int id) =>
        Ok(await profileService.Followers(ViewerId(), id));

    /// <summary>
    /// The users a user follows.
    /// </summary>
    /// <param name="id">
    /// Identifier of the user.
    /// </param>
    /// <returns>
    /// 200 OK with the followed users, the most recent first.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpGet("{id:int}/following")]
    public async Task<ActionResult<List<UserSummaryResponse>>> Following(int id) =>
        Ok(await profileService.Following(ViewerId(), id));

    /// <summary>
    /// Makes the signed-in user follow another.
    /// </summary>
    /// <param name="id">
    /// Identifier of the user to follow.
    /// </param>
    /// <returns>
    /// 200 OK with the profile of the followed user.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The user tried to follow themselves.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpPut("{id:int}/follow")]
    public async Task<ActionResult<ProfileResponse>> Follow(int id) =>
        Ok(await profileService.Follow(ViewerId(), id));

    /// <summary>
    /// Makes the signed-in user stop following another.
    /// </summary>
    /// <param name="id">
    /// Identifier of the user to stop following.
    /// </param>
    /// <returns>
    /// 200 OK with the profile of that user.
    /// </returns>
    /// <exception cref="NotFoundException">
    /// There is no such user.
    /// </exception>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpDelete("{id:int}/follow")]
    public async Task<ActionResult<ProfileResponse>> Unfollow(int id) =>
        Ok(await profileService.Unfollow(ViewerId(), id));

    /// <summary>
    /// Identifier of the signed-in user, from the access token.
    /// </summary>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    private int ViewerId()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(subject, out var userId))
        {
            throw new UnauthorizedException("The token has no valid user.");
        }

        return userId;
    }
}
