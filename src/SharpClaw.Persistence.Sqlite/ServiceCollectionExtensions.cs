using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Abstractions.Persistence;
using SharpClaw.Persistence.Core;
using SharpClaw.Persistence.Sqlite.Repositories;

namespace SharpClaw.Persistence.Sqlite;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqlitePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=sharpclaw.db";

        services.AddDbContext<SharpClawDbContext>((provider, options) =>
        {
            options.UseSqlite(connectionString);
        });

        // Register generic repository
        services.AddScoped(typeof(IRepository<>), typeof(SqliteRepository<>));

        return services;
    }
}
