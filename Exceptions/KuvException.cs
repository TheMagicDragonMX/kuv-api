namespace kuv_api.Exceptions;

/// <summary>
/// Base class for expected, client-facing errors. Throw one of its subclasses from any layer;
/// <see cref="KuvExceptionHandler"/> turns it into an HTTP response using <see cref="StatusCode"/>
/// and <see cref="Exception.Message"/>.
/// </summary>
/// <param name="statusCode">
/// HTTP status code returned to the client.
/// </param>
/// <param name="message">Message returned to the client in the <c>message</c> field.</param>
public class KuvException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
