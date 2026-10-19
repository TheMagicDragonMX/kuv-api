namespace kuv_api.Exceptions;

public sealed class ForbiddenException(string message) : KuvException(StatusCodes.Status403Forbidden, message);
