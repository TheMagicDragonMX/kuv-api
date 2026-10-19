namespace kuv_api.Exceptions;

public sealed class UnauthorizedException(string message) : KuvException(StatusCodes.Status401Unauthorized, message);
