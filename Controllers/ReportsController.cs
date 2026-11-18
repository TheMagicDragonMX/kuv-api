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
/// Endpoints for submitting and reviewing moderation reports.
/// </summary>
[ApiController]
[Authorize]
[Route("reports")]
public class ReportsController(ReportService reportService) : ControllerBase
{
	private readonly ReportService reportService = reportService;

	/// <summary>
	/// Submits a new moderation report against an animation, user profile, or comment.
	/// </summary>
	/// <param name="request">
	/// Target type, target id, category, and optional details.
	/// </param>
	/// <returns>
	/// 201 Created with the persisted report summary.
	/// </returns>
	[HttpPost]
	public async Task<ActionResult<ReportResponse>> Create(CreateReportRequest request)
	{
		var created = await reportService.CreateReport(ViewerId(), request);
		return Created($"reports/{created.Id}", created);
	}

	/// <summary>
	/// Returns all reports submitted by the signed-in user.
	/// </summary>
	/// <returns>
	/// 200 OK with the list of submitted reports.
	/// </returns>
	[HttpGet("me")]
	public async Task<ActionResult<List<ReportResponse>>> Mine() =>
		Ok(await reportService.GetMyReports(ViewerId()));

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

