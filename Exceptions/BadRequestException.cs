namespace kuv_api.Exceptions;

/// <summary>
/// The request is invalid (HTTP 400).
/// </summary>
public sealed class BadRequestException(string message) : KuvException(StatusCodes.Status400BadRequest, message);
