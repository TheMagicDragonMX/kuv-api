namespace kuv_api.Requests;

/// <summary>
/// New profile data of the signed-in user. A null or blank field clears it.
/// </summary>
/// <param name="Description">
/// What the user tells about themselves.
/// </param>
/// <param name="Photo">
/// Profile picture as a data URL (<c>data:image/...;base64,...</c>).
/// </param>
public sealed record UpdateProfileRequest(string? Description, string? Photo);
