using kuv_api.Data;
using kuv_api.Models.Exceptions;
using kuv_api.Models.Requests;
using kuv_api.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace kuv_api.Services;

/// <summary>
/// What only super admins can do (<see cref="Models.Entities.User.IsSuperAdmin"/>). Every method checks the viewer
/// first, then does the work through the service it belongs to.
/// </summary>
public sealed class AdminService(KuvDbContext dbContext, CreationService creationService)
{
    private readonly KuvDbContext dbContext = dbContext;

    private readonly CreationService creationService = creationService;

    /// <summary>
    /// Every creation of every user, the most recent first.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <exception cref="ForbiddenException">
    /// The viewer is not a super admin.
    /// </exception>
    public async Task<List<AdminCreationResponse>> GetAllCreations(int viewerId)
    {
        await RequireSuperAdmin(viewerId);

        return await creationService.GetAllCreations();
    }

    /// <summary>
    /// Replaces the preview (videos and thumbnail) of any creation, whoever owns it.
    /// </summary>
    /// <param name="viewerId">
    /// The signed-in user.
    /// </param>
    /// <param name="creationId">
    /// The creation whose preview is replaced.
    /// </param>
    /// <param name="request">
    /// The two videos and the thumbnail.
    /// </param>
    /// <exception cref="ForbiddenException">
    /// The viewer is not a super admin.
    /// </exception>
    /// <exception cref="NotFoundException">
    /// The creation does not exist.
    /// </exception>
    /// <exception cref="BadRequestException">
    /// A file is missing or the thumbnail is not a small PNG.
    /// </exception>
    public async Task<CreationResponse> SetPreview(int viewerId, int creationId, SetPreviewRequest request)
    {
        await RequireSuperAdmin(viewerId);

        return await creationService.SetPreview(creationId, request);
    }

    /// <summary>
    /// Lets only super admins through.
    /// </summary>
    /// <exception cref="ForbiddenException">
    /// The viewer is not a super admin (or no longer exists).
    /// </exception>
    private async Task RequireSuperAdmin(int viewerId)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Id == viewerId && user.IsSuperAdmin))
        {
            throw new ForbiddenException("Solo los super administradores pueden hacer esto.");
        }
    }
}
