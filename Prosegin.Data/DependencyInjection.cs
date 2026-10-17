using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Prosegin.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
        var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
        var dbName = Environment.GetEnvironmentVariable("DB_NAME");
        var dbUser = Environment.GetEnvironmentVariable("DB_USER");
        var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(dbHost) && !string.IsNullOrWhiteSpace(dbName) && !string.IsNullOrWhiteSpace(dbUser))
        {
            connectionString = $"Server={dbHost};Port={dbPort};Database={dbName};User={dbUser};Password={dbPassword};";
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Data Source=prosegin.db";
        }

        services.AddDbContext<ProseginDbContext>(options =>
        {
            if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
                || connectionString.Contains("Filename=", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
                return;
            }

            ServerVersion serverVersion;
            try
            {
                serverVersion = ServerVersion.AutoDetect(connectionString);
            }
            catch
            {
                serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
            }

            options.UseMySql(connectionString, serverVersion);
        });

        return services;
    }
}
