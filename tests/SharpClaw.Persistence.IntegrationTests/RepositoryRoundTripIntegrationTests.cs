using SharpClaw.Persistence.Abstractions;
using SharpClaw.Persistence.Postgres;
using SharpClaw.Persistence.Sqlite;

namespace SharpClaw.Persistence.IntegrationTests;

public class RepositoryRoundTripIntegrationTests
{
    private static (IRunStore runs, ISessionStore sessions, IConfigRevisionStore configs, IAuditStore audits) CreateStores(string provider)
    {
        return provider switch
        {
            "sqlite" => (new SqliteRunStore(), new SqliteSessionStore(), new SqliteConfigRevisionStore(), new SqliteAuditStore()),
            "postgres" => (new PostgresRunStore(), new PostgresSessionStore(), new PostgresConfigRevisionStore(), new PostgresAuditStore()),
            _ => throw new InvalidOperationException("Unknown provider")
        };
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task RepositoryRoundTrip_PersistsAndOverwrites(string provider)
    {
        IRepository<string> repository = provider switch
        {
            "sqlite" => new SqliteRepository<string>(),
            "postgres" => new PostgresRepository<string>(),
            _ => throw new InvalidOperationException("Unknown provider")
        };

        await repository.UpsertAsync("run-1", "started");
        var first = await repository.GetAsync("run-1");
        Assert.Equal("started", first);

        await repository.UpsertAsync("run-1", "completed");
        var second = await repository.GetAsync("run-1");
        Assert.Equal("completed", second);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task DomainStores_RoundTripAcrossComponents(string provider)
    {
        var (runs, sessions, configs, audits) = CreateStores(provider);

        await runs.UpsertAsync(new RunRecord("run-9", RunStatus.Completed, DateTimeOffset.UtcNow));
        await sessions.UpsertAsync(new SessionRecord("session-9", "main", DateTimeOffset.UtcNow));
        await configs.UpsertAsync(new ConfigRevisionRecord("rev-9", "hash-9", DateTimeOffset.UtcNow, true));
        await audits.AppendAsync(new AuditEventRecord("evt-9", "runs", "completed", DateTimeOffset.UtcNow));

        var run = await runs.GetAsync("run-9");
        var session = await sessions.GetAsync("session-9");
        var config = await configs.GetAsync("rev-9");
        var runAudit = await audits.ListByCategoryAsync("runs");

        Assert.NotNull(run);
        Assert.NotNull(session);
        Assert.NotNull(config);
        Assert.Single(runAudit);
    }
}
