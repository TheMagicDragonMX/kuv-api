using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace kuv_api.Data;

public sealed class KuvDbContextFactory : IDesignTimeDbContextFactory<KuvDbContext>
{
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