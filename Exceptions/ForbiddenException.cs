namespace kuv_api.Exceptions;

/// <summary>
/// The caller is authenticated but not allowed to do this (HTTP 403).
/// </summary>
public sealed class ForbiddenException(string message) : KuvException(StatusCodes.Status403Forbidden, message);
