namespace kuv_api.Exceptions;

public sealed class NotFoundException(string message) : KuvException(StatusCodes.Status404NotFound, message);
