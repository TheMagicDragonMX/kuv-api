namespace kuv_api.Exceptions;

/// <summary>
/// The operation conflicts with existing state, e.g. a duplicate (HTTP 409).
/// </summary>
public sealed class ConflictException(string message) : KuvException(StatusCodes.Status409Conflict, message);
