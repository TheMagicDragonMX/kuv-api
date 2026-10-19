using Microsoft.AspNetCore.Diagnostics;

namespace kuv_api.Exceptions;

/// <summary>
/// Global exception handler. Maps <see cref="KuvException"/> to its status code and a
/// <c>{ "message": "..." }</c> body; any other exception is logged and returned as a generic 500.
/// </summary>
public sealed class KuvExceptionHandler(ILogger<KuvExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is KuvException kuvException)
        {
            httpContext.Response.StatusCode = kuvException.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(new { message = kuvException.Message }, cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled exception");
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new { message = "Ocurrió un error inesperado." }, cancellationToken);
        return true;
    }
}
