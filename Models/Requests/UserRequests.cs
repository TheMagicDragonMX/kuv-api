namespace kuv_api.Models.Requests;

/// <summary>
/// New profile data of the signed-in user. A null or blank bio clears it.
/// </summary>
/// <param name="Bio">
/// What the user tells about themselves.
/// </param>
public sealed record UpdateProfileRequest(string? Bio);

/// <summary>
/// The picture to use as the profile picture of the signed-in user.
/// </summary>
public sealed class UpdatePhotoRequest
{
	/// <summary>
	/// The image file: PNG, JPEG, WebP or GIF.
	/// </summary>
	public required IFormFile Photo { get; set; }
}
