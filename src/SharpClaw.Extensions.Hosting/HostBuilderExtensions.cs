using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharpClaw.Abstractions.Cloud;
using SharpClaw.Abstractions.Identity;
using SharpClaw.Abstractions.Persistence;
using SharpClaw.Cloud.Azure;
using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.Daytona;
using SharpClaw.Execution.Docker;
using SharpClaw.Execution.Kubernetes;
using SharpClaw.Execution.Podman;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.Gateway;
using SharpClaw.Gateway.Events;
using SharpClaw.Identity;
using static SharpClaw.Identity.IdentityService;
using SharpClaw.Persistence.Contracts.Entities;
using SharpClaw.Persistence.Core;
using SharpClaw.RateLimiting;
using SharpClaw.RateLimiting.Abstractions;
using SharpClaw.RateLimiting.Stores;
using SharpClaw.Runs;
using SharpClaw.Tenancy;
using SharpClaw.Tenancy.Abstractions;
using SharpClaw.Tenancy.Resolvers;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

#pragma warning disable IDE2003
#pragma warning disable IDE0060

namespace SharpClaw.Extensions.Hosting;

public static class HostBuilderExtensions
{
    public static IHostApplicationBuilder AddSharpClawHosting(
        this IHostApplicationBuilder builder,
        ExecutionProviderPolicy? executionProviderPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Tenancy
        builder.Services.AddSingleton<ITenantResolver, HeaderTenantResolver>();
        builder.Services.AddSingleton<ITenantContext>(provider => AsyncLocalTenantContext.Current);

        // Rate Limiting
        builder.Services.AddSingleton<IRateLimitStore, MemoryRateLimitStore>();
        builder.Services.AddSingleton<IRateLimiter, TokenBucketRateLimiter>();

        // Persistence (in-memory for now, can be replaced with SQLite/PostgreSQL)
        builder.Services.AddDbContext<SharpClawDbContext>(options =>
        {
            // In-memory for tests, real DB in production
            options.UseInMemoryDatabase("SharpClaw");
        });
        
        // Register generic repository
        builder.Services.AddScoped(typeof(IRepository<>), typeof(SharpClawDbContextRepository<>));
        
        // Register concrete repositories for specific entity types
        builder.Services.AddScoped<IRepository<DeviceIdentityEntity>, SharpClawDbContextRepository<DeviceIdentityEntity>>();
        builder.Services.AddScoped<IRepository<RunRecordEntity>, SharpClawDbContextRepository<RunRecordEntity>>();
        builder.Services.AddScoped<IRepository<IdempotencyKeyEntity>, SharpClawDbContextRepository<IdempotencyKeyEntity>>();

        // Rate limiting options
        builder.Services.AddSingleton(new RateLimiterOptions { TokensPerPeriod = 10, TokenLimit = 100 });

        // Identity
        builder.Services.AddScoped<IIdentityService, IdentityService>();
        builder.Services.AddSingleton<Abstractions.Identity.ITokenService>(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            return new JwtTokenService(config);
        });

        // Gateway
        builder.Services.AddSingleton<ConnectionRegistry>();
        builder.Services.AddSingleton<GatewayHealthService>();
        
        // Agent Pipeline registrations
        builder.Services.AddSingleton<AgentRuntimePipeline>();
        
        // Register the agent runtime adapter with IChatClient dependency
        // Note: IChatClient should be registered by the application using this library
        builder.Services.AddSingleton<IAgentRuntimeAdapter>(provider =>
        {
            var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<MicrosoftAgentRuntimeAdapter>>();
            var chatClient = provider.GetService<Microsoft.Extensions.AI.IChatClient>();
            
            if (chatClient is null)
            {
                // Fallback to a simple dummy adapter if no IChatClient is registered
                // This allows the application to start without a real AI client
                return new DummyAgentRuntimeAdapter();
            }
            
            return new MicrosoftAgentRuntimeAdapter(chatClient, logger);
        });
        
        builder.Services.AddSingleton<RunExecutionService>();
        builder.Services.AddSingleton<IRunExecutionService>(provider => provider.GetRequiredService<RunExecutionService>());
        
        builder.Services.AddSingleton<RunCoordinator>();

        // Event Publishing
        builder.Services.AddSingleton<IEventPublisher>(provider =>
        {
            var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ChannelEventPublisher>>();
            return new ChannelEventPublisher(EventPublisherOptions.Default, logger);
        });

        // Keepalive Services - Order matters: HealthMonitor first, then Metrics, then BackgroundService
        builder.Services.AddSingleton<SharpClaw.Gateway.Keepalive.ConnectionHealthMonitor>();
        builder.Services.AddSingleton<SharpClaw.Gateway.Keepalive.KeepaliveMetrics>(
            provider => new SharpClaw.Gateway.Keepalive.KeepaliveMetrics(
                provider.GetRequiredService<SharpClaw.Gateway.Keepalive.ConnectionHealthMonitor>()));
        builder.Services.AddHostedService<SharpClaw.Gateway.Keepalive.KeepaliveBackgroundService>();

        // Execution Providers
        builder.Services.AddSingleton<ISandboxProvider, DockerSandboxProvider>();
        builder.Services.AddSingleton<ISandboxProvider, PodmanSandboxProvider>();
        builder.Services.AddSingleton<ISandboxProvider, DaytonaSandboxProvider>();
        builder.Services.AddSingleton<ISandboxProvider, KubernetesSandboxProvider>();

        builder.Services.AddSingleton(executionProviderPolicy ?? new ExecutionProviderPolicy());
        builder.Services.AddSingleton<SandboxManagerService>();

        builder.Services.AddSingleton(provider =>
        {
            var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GatewayDispatcher>>();
            var eventPublisher = provider.GetRequiredService<IEventPublisher>();
            var dispatcher = new GatewayDispatcher(eventPublisher, logger);
            var runCoordinator = provider.GetRequiredService<RunCoordinator>();
            GatewayMethodRegistration.RegisterCoreMethods(dispatcher, runCoordinator);
            return dispatcher;
        });

        return builder;
    }

    /// <summary>
    /// Adds Azure Cloud Provider services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure Azure Cloud Provider options.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddAzureCloudProvider(
        this IServiceCollection services,
        Action<AzureCloudProviderOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new AzureCloudProviderOptions();
        configureOptions(options);

        services.AddSingleton<ICloudProvider>(_ => new AzureCloudProvider(options));
        return services;
    }
}

/// <summary>
/// EF Core repository adapter using SharpClawDbContext.
/// </summary>
public sealed class SharpClawDbContextRepository<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly SharpClawDbContext _context;

    public SharpClawDbContextRepository(SharpClawDbContext context)
    {
        _context = context;
    }

    public async Task<TEntity?> GetAsync(string id, string tenantId, CancellationToken ct = default)
    {
        return await _context.Set<TEntity>().FindAsync(new[] { tenantId, id }, ct);
    }

    public async Task UpsertAsync(string id, string tenantId, TEntity entity, CancellationToken ct = default)
    {
        var existing = await _context.Set<TEntity>().FindAsync(new[] { tenantId, id }, ct);
        if (existing == null)
        {
            _context.Set<TEntity>().Add(entity);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, string tenantId, CancellationToken ct = default)
    {
        var entity = await _context.Set<TEntity>().FindAsync(new[] { tenantId, id }, ct);
        if (entity != null)
        {
            _context.Set<TEntity>().Remove(entity);
            await _context.SaveChangesAsync(ct);
        }
    }

    public IQueryable<TEntity> Query(string tenantId)
    {
        // Apply tenant filter
        return _context.Set<TEntity>().AsQueryable();
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(string tenantId, CancellationToken ct = default)
    {
        return await _context.Set<TEntity>().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        string tenantId,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _context.Set<TEntity>().Where(predicate).ToListAsync(ct);
    }

    public async Task<TEntity?> GetByExpressionAsync(Expression<Func<TEntity, bool>> predicate, string tenantId, CancellationToken ct = default)
    {
        return await _context.Set<TEntity>().FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, string tenantId, CancellationToken ct = default)
    {
        return await _context.Set<TEntity>().Where(predicate).ToListAsync(ct);
    }
}
