using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace kuv_api.Data;

/// <summary>
/// Creates <see cref="KuvDbContext"/> instances for design-time tooling such as <c>dotnet ef migrations</c>.
/// </summary>
public sealed class KuvDbContextFactory : IDesignTimeDbContextFactory<KuvDbContext>
{
    /// <summary>
    /// Builds a context from the appsettings files and environment variables.
    /// </summary>
    /// <param name="args">
    /// Arguments passed by the EF tooling.
    /// </param>
    /// <returns>
    /// A context configured to use PostgreSQL.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The connection string is missing.
    /// </exception>
    public KuvDbContext CreateDbContext(string[] args)
    {
        // Build configuration
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get connection string
        var connectionString = DatabaseConnection.GetConnectionString(configuration);

        // Build context
        var optionsBuilder = new DbContextOptionsBuilder<KuvDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new KuvDbContext(optionsBuilder.Options);
    }
}