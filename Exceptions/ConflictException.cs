namespace kuv_api.Exceptions;

public sealed class ConflictException(string message) : KuvException(StatusCodes.Status409Conflict, message);
