namespace kuv_api.Exceptions;

public class KuvException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
