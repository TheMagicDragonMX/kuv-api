using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Requests;
using kuv_api.Models.Responses;
using kuv_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kuv_api.Controllers;

/// <summary>
/// The super admin panel. Every endpoint needs the access token of a super admin; anyone else gets 403.
/// </summary>
[ApiController]
[Authorize]
[Route("admin")]
public class AdminController(AdminService adminService) : ControllerBase
{
	private readonly AdminService adminService = adminService;

	/// <summary>
	/// Every creation of every user, public or private, the most recent first, with its author.
	/// </summary>
	/// <exception cref="ForbiddenException">
	/// The viewer is not a super admin.
	/// </exception>
	[HttpGet("creations")]
	public async Task<ActionResult<List<AdminCreationResponse>>> GetAllCreations()
	{
		return Ok(await adminService.GetAllCreations(ViewerId()));
	}

	/// <summary>
	/// Replaces the preview of any creation: <c>videoDark</c>, <c>videoLight</c> and <c>thumbnail</c>, all required.
	/// Nothing else of the creation changes.
	/// </summary>
	/// <exception cref="ForbiddenException">
	/// The viewer is not a super admin.
	/// </exception>
	/// <exception cref="NotFoundException">
	/// The creation does not exist.
	/// </exception>
	/// <exception cref="BadRequestException">
	/// A file is missing or the thumbnail is not a small PNG.
	/// </exception>
	[HttpPut("creations/{id:int}/preview")]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(100 * 1024 * 1024)]
	public async Task<ActionResult<CreationResponse>> SetPreview(int id, [FromForm] SetPreviewRequest request)
	{
		return Ok(await adminService.SetPreview(ViewerId(), id, request));
	}

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
