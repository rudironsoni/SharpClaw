using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Abstractions.Persistence;
using SharpClaw.Persistence.Core;
using SharpClaw.Persistence.PostgreSQL.Repositories;

namespace SharpClaw.Persistence.PostgreSQL;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostgreSQLPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<SharpClawDbContext>((provider, options) =>
        {
            var connectionString = configuration.GetConnectionString("PostgreSQL")
                ?? throw new InvalidOperationException("PostgreSQL connection string not configured");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsqlOptions.MigrationsAssembly("SharpClaw.Persistence.PostgreSQL");
            });
        });

        services.AddScoped(typeof(IRepository<>), typeof(PostgreSQLRepository<>));

        return services;
    }

    public static IServiceCollection AddPostgreSQLPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<SharpClawDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsqlOptions.MigrationsAssembly("SharpClaw.Persistence.PostgreSQL");
            });
        });

        services.AddScoped(typeof(IRepository<>), typeof(PostgreSQLRepository<>));

        return services;
    }
}
