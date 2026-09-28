using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace kuv_api.Data;

public sealed class KuvDbContextFactory : IDesignTimeDbContextFactory<KuvDbContext>
{
    public KuvDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<KuvDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = DatabaseConnection.GetConnectionString(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<KuvDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new KuvDbContext(optionsBuilder.Options);
    }
}