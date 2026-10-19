namespace kuv_api.Exceptions;

public sealed class BadRequestException(string message) : KuvException(StatusCodes.Status400BadRequest, message);
