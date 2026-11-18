using kuv_api.Models.Responses;
using kuv_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kuv_api.Controllers;

/// <summary>
/// The categories of the feed, the home page of the web app. Each category has its own endpoint.
/// </summary>
[ApiController]
[Authorize]
[Route("feed")]
public class FeedController(FeedService feedService) : ControllerBase
{
    private readonly FeedService feedService = feedService;

    /// <summary>
    /// The newest public creations, the most recent first.
    /// </summary>
    /// <param name="limit">
    /// How many to answer at most (default <see cref="FeedService.DefaultLimit"/>, never more than <see cref="FeedService.MaxLimit"/>).
    /// </param>
    /// <returns>
    /// 200 OK with the creations.
    /// </returns>
    [HttpGet("new")]
    public async Task<ActionResult<List<FeedCreationResponse>>> New([FromQuery] int limit = FeedService.DefaultLimit)
    {
        return Ok(await feedService.NewCreations(limit));
    }
}
