using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using kuv_api.Exceptions;
using kuv_api.Requests;
using kuv_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kuv_api.Controllers;

/// <summary>
/// Endpoints for managing user creations and their videos.
/// </summary>
[ApiController]
[Authorize]
[Route("creations")]
public class CreationsController(CreationService creationService) : ControllerBase
{
	private readonly CreationService creationService = creationService;

	/// <summary>
	/// Returns all creations of the current authenticated user.
	/// </summary>
	[HttpGet("me")]
	public async Task<ActionResult<List<CreationResponse>>> GetMyCreations()
	{
		return Ok(await creationService.GetUserCreations(ViewerId()));
	}

	/// <summary>
	/// Returns a specific creation by ID.
	/// </summary>
	[HttpGet("{id:int}")]
	public async Task<ActionResult<CreationResponse>> GetCreation(int id)
	{
		return Ok(await creationService.GetCreation(ViewerId(), id));
	}

	/// <summary>
	/// Streams or serves the video file for a creation.
	/// </summary>
	[HttpGet("{id:int}/video")]
	public async Task<IActionResult> GetVideo(int id)
	{
		var (filePath, contentType) = await creationService.GetCreationVideo(ViewerId(), id);
		return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
	}

	/// <summary>
	/// Creates a new creation for the current user.
	/// </summary>
	[HttpPost]
	[Consumes("multipart/form-data")]
	public async Task<ActionResult<CreationResponse>> Create([FromForm] CreateCreationRequest request)
	{
		var response = await creationService.Create(ViewerId(), request);
		return CreatedAtAction(nameof(GetCreation), new { id = response.Id }, response);
	}

	/// <summary>
	/// Updates an existing creation.
	/// </summary>
	[HttpPut("{id:int}")]
	[Consumes("multipart/form-data")]
	public async Task<ActionResult<CreationResponse>> Update(int id, [FromForm] UpdateCreationRequest request)
	{
		return Ok(await creationService.Update(ViewerId(), id, request));
	}

	/// <summary>
	/// Deletes a creation and its video file.
	/// </summary>
	[HttpDelete("{id:int}")]
	public async Task<IActionResult> Delete(int id)
	{
		await creationService.Delete(ViewerId(), id);
		return NoContent();
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

