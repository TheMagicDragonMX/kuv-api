using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using kuv_api.Exceptions;
using kuv_api.Requests;
using kuv_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kuv_api.Controllers;

/// <summary>
/// Profiles and follows. Every endpoint needs the access token of a signed-in user.
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
    /// Changes the description and picture of the signed-in user.
    /// </summary>
    /// <param name="request">
    /// The new data; a blank field is cleared.
    /// </param>
    /// <returns>
    /// 200 OK with the profile after the change.
    /// </returns>
    /// <exception cref="BadRequestException">
    /// The description is too long, or the picture is not valid.
    /// </exception>
    /// <exception cref="UnauthorizedException">
    /// The token has no valid user.
    /// </exception>
    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateMe(UpdateProfileRequest request) =>
        Ok(await profileService.UpdateProfile(ViewerId(), request));

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
