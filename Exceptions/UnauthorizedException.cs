namespace kuv_api.Exceptions;

/// <summary>
/// The caller is not authenticated or the credentials are wrong (HTTP 401).
/// </summary>
public sealed class UnauthorizedException(string message) : KuvException(StatusCodes.Status401Unauthorized, message);
