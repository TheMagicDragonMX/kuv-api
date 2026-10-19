namespace kuv_api.Exceptions;

/// <summary>
/// The requested resource does not exist (HTTP 404).
/// </summary>
public sealed class NotFoundException(string message) : KuvException(StatusCodes.Status404NotFound, message);
