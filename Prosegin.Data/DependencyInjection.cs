using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Prosegin.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Priorizar variables de entorno (.env)
        var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
        var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "3306";
        var dbName = Environment.GetEnvironmentVariable("DB_NAME");
        var dbUser = Environment.GetEnvironmentVariable("DB_USER");
        var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

        string connectionString;
        // Priorizar appsettings.json para desarrollo local
        connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        
        // Si hay variables de entorno configuradas, usarlas (para producción)
        if (!string.IsNullOrEmpty(dbHost) && !string.IsNullOrEmpty(dbName) && !string.IsNullOrEmpty(dbUser))
        {
            connectionString = $"Server={dbHost};Port={dbPort};Database={dbName};User={dbUser};Password={dbPassword};";
        }

        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddDbContext<ProseginDbContext>(options =>
            {
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
        }

        return services;
    }
}
