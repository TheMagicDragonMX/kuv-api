using kuv_api.Data;
using kuv_api.Models.Entities;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Requests;
using kuv_api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// Handles moderation reports for animations, user profiles, and comments,
/// enforcing strict no-self-reporting rules.
/// </summary>
public sealed class ReportService(KuvDbContext dbContext)
{
	/// <summary>
	/// Maximum length of the optional report explanation.
	/// </summary>
	public const int MaxDetailsLength = 500;

	private static readonly HashSet<string> AnimationCategories =
	[
		"obscene",
		"offensive_language",
		"harassment",
		"photosensitivity_risk",
		"spam",
		"plagiarism",
		"other"
	];

	private static readonly HashSet<string> UserOrCommentCategories =
	[
		"impersonation",
		"inappropriate_profile",
		"harassment",
		"hate_speech",
		"spam_phishing",
		"doxxing",
		"trolling",
		"other"
	];

	private readonly KuvDbContext dbContext = dbContext;

	/// <summary>
	/// Submits a moderation report against an animation, user profile, or comment.
	/// </summary>
	/// <param name="reportingUserId">
	/// Identifier of the signed-in user submitting the report.
	/// </param>
	/// <param name="request">
	/// Report target, category, and optional details.
	/// </param>
	/// <returns>
	/// The persisted report summary.
	/// </returns>
	/// <exception cref="BadRequestException">
	/// The target type, category, or details are invalid, or the user attempted to self-report.
	/// </exception>
	/// <exception cref="NotFoundException">
	/// The reported creation, user, or comment does not exist.
	/// </exception>
	/// <exception cref="ConflictException">
	/// The user already has a pending report for the same target.
	/// </exception>
	public async Task<ReportResponse> CreateReport(int reportingUserId, CreateReportRequest request)
	{
		var targetType = request.TargetType?.Trim().ToLowerInvariant() ?? string.Empty;
		var category = request.Category?.Trim().ToLowerInvariant() ?? string.Empty;
		var details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim();

		if (targetType is not ("animation" or "user" or "comment"))
		{
			throw new BadRequestException("El tipo de objetivo del reporte debe ser animation, user o comment.");
		}

		var validCategories = targetType == "animation" ? AnimationCategories : UserOrCommentCategories;

		if (!validCategories.Contains(category))
		{
			throw new BadRequestException("La categoría de reporte seleccionada no es válida.");
		}

		if (category == "other" && details is null)
		{
			throw new BadRequestException("Por favor explica el motivo del reporte.");
		}

		if (details is not null && details.Length > MaxDetailsLength)
		{
			throw new BadRequestException($"Los detalles no pueden superar los {MaxDetailsLength} caracteres.");
		}

		var reportedUserId = await ResolveReportedUserId(targetType, request.TargetId);

		if (reportedUserId == reportingUserId)
		{
			throw new BadRequestException("No puedes reportar tu propio contenido, comentario o perfil.");
		}

		var duplicatePending = await dbContext.Reports.AnyAsync(report =>
			report.ReportingUserId == reportingUserId &&
			report.TargetType == targetType &&
			report.TargetId == request.TargetId &&
			report.Status == "pending"
		);

		if (duplicatePending)
		{
			throw new ConflictException("Ya has enviado un reporte pendiente para este elemento.");
		}

		var report = new Report
		{
			ReportingUserId = reportingUserId,
			TargetType = targetType,
			TargetId = request.TargetId,
			ReportedUserId = reportedUserId,
			Category = category,
			Details = details
		};

		dbContext.Reports.Add(report);
		await dbContext.SaveChangesAsync();

		return ToResponse(report);
	}

	/// <summary>
	/// Returns all reports submitted by the signed-in user, newest first.
	/// </summary>
	/// <param name="reportingUserId">
	/// Identifier of the signed-in user.
	/// </param>
	/// <returns>
	/// List of reports created by the user.
	/// </returns>
	public async Task<List<ReportResponse>> GetMyReports(int reportingUserId)
	{
		return await dbContext.Reports
			.Where(report => report.ReportingUserId == reportingUserId)
			.OrderByDescending(report => report.CreatedAt)
			.ThenByDescending(report => report.Id)
			.Select(report => new ReportResponse(
				report.Id,
				report.TargetType,
				report.TargetId,
				report.ReportedUserId,
				report.Category,
				report.Details,
				report.Status,
				report.CreatedAt
			))
			.ToListAsync();
	}

	private async Task<int> ResolveReportedUserId(string targetType, int targetId)
	{
		return targetType switch
		{
			"user" => await dbContext.Users
				.Where(user => user.Id == targetId)
				.Select(user => user.Id)
				.FirstOrDefaultAsync() is var userId && userId > 0
					? userId
					: throw new NotFoundException("El usuario reportado no existe."),

			"animation" => await dbContext.Creations
				.Where(creation => creation.Id == targetId)
				.Select(creation => (int?)creation.UserId)
				.FirstOrDefaultAsync()
				?? throw new NotFoundException("La creación reportada no existe."),

			"comment" => await dbContext.Comments
				.Where(comment => comment.Id == targetId)
				.Select(comment => (int?)comment.UserId)
				.FirstOrDefaultAsync()
				?? throw new NotFoundException("El comentario reportado no existe."),

			_ => throw new BadRequestException("Tipo de reporte inválido.")
		};
	}

	private static ReportResponse ToResponse(Report report) => new(
		report.Id,
		report.TargetType,
		report.TargetId,
		report.ReportedUserId,
		report.Category,
		report.Details,
		report.Status,
		report.CreatedAt
	);
}

