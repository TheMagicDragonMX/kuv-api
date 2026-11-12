namespace kuv_api.Models.Exceptions;

/// <summary>
/// The operation conflicts with existing state, e.g. a duplicate (HTTP 409).
/// </summary>
public sealed class ConflictException(string message) : KuvException(StatusCodes.Status409Conflict, message);
