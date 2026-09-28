namespace kuv_api.Data;

/// <summary>
/// Reads the PostgreSQL connection string from the application's configuration sources.
/// The application expects this value to be provided via a standard .NET configuration entry,
/// typically through user secrets or the ConnectionStrings__DefaultConnection environment variable.
/// </summary>
public static class DatabaseConnection
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var value = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is missing");
        }

        return value;
    }
}