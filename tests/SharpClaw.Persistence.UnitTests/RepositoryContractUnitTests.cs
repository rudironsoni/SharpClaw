using SharpClaw.Persistence.Abstractions;
using SharpClaw.Persistence.Postgres;
using SharpClaw.Persistence.Sqlite;

namespace SharpClaw.Persistence.UnitTests;

public class RepositoryContractUnitTests
{
    private static IRunStore CreateRunStore(string provider)
    {
        return provider switch
        {
            "sqlite" => new SqliteRunStore(),
            "postgres" => new PostgresRunStore(),
            _ => throw new InvalidOperationException("Unknown provider")
        };
    }

    private static ISessionStore CreateSessionStore(string provider)
    {
        return provider switch
        {
            "sqlite" => new SqliteSessionStore(),
            "postgres" => new PostgresSessionStore(),
            _ => throw new InvalidOperationException("Unknown provider")
        };
    }

    private static IConfigRevisionStore CreateConfigStore(string provider)
    {
        return provider switch
        {
            "sqlite" => new SqliteConfigRevisionStore(),
            "postgres" => new PostgresConfigRevisionStore(),
            _ => throw new InvalidOperationException("Unknown provider")
        };
    }

    private static IAuditStore CreateAuditStore(string provider)
    {
        return provider switch
        {
            "sqlite" => new SqliteAuditStore(),
            "postgres" => new PostgresAuditStore(),
            _ => throw new InvalidOperationException("Unknown provider")
        };
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task GetAsync_WhenMissingKey_ReturnsNull(string provider)
    {
        IRepository<string> repository = provider switch
        {
            "sqlite" => new SqliteRepository<string>(),
            "postgres" => new PostgresRepository<string>(),
            _ => throw new InvalidOperationException("Unknown provider")
        };

        var value = await repository.GetAsync("missing");

        Assert.Null(value);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task UpsertAsync_WithEmptyId_Throws(string provider)
    {
        IRepository<string> repository = provider switch
        {
            "sqlite" => new SqliteRepository<string>(),
            "postgres" => new PostgresRepository<string>(),
            _ => throw new InvalidOperationException("Unknown provider")
        };

        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpsertAsync("", "value"));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task GetAndUpsert_RespectCancellation(string provider)
    {
        IRepository<string> repository = provider switch
        {
            "sqlite" => new SqliteRepository<string>(),
            "postgres" => new PostgresRepository<string>(),
            _ => throw new InvalidOperationException("Unknown provider")
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.GetAsync("id", cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.UpsertAsync("id", "v", cts.Token));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task RunStore_UpsertAndGet_Works(string provider)
    {
        var store = CreateRunStore(provider);
        var record = new RunRecord("run-1", RunStatus.Running, DateTimeOffset.UtcNow, "idem-1");

        await store.UpsertAsync(record);
        var loaded = await store.GetAsync("run-1");

        Assert.NotNull(loaded);
        Assert.Equal(RunStatus.Running, loaded!.Status);
        Assert.Equal("idem-1", loaded.IdempotencyKey);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task SessionStore_UpsertAndGet_Works(string provider)
    {
        var store = CreateSessionStore(provider);
        var record = new SessionRecord("session-1", "main", DateTimeOffset.UtcNow, "msg-1");

        await store.UpsertAsync(record);
        var loaded = await store.GetAsync("session-1");

        Assert.NotNull(loaded);
        Assert.Equal("main", loaded!.Scope);
        Assert.Equal("msg-1", loaded.LastMessageId);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task ConfigStore_UpsertAndGet_Works(string provider)
    {
        var store = CreateConfigStore(provider);
        var record = new ConfigRevisionRecord("rev-1", "hash-1", DateTimeOffset.UtcNow, IsActive: true);

        await store.UpsertAsync(record);
        var loaded = await store.GetAsync("rev-1");

        Assert.NotNull(loaded);
        Assert.Equal("hash-1", loaded!.Hash);
        Assert.True(loaded.IsActive);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    public async Task AuditStore_AppendsAndFiltersByCategory(string provider)
    {
        var store = CreateAuditStore(provider);
        await store.AppendAsync(new AuditEventRecord("1", "auth", "login", DateTimeOffset.UtcNow));
        await store.AppendAsync(new AuditEventRecord("2", "auth", "logout", DateTimeOffset.UtcNow));
        await store.AppendAsync(new AuditEventRecord("3", "runs", "start", DateTimeOffset.UtcNow));

        var authEvents = await store.ListByCategoryAsync("auth");

        Assert.Equal(2, authEvents.Count);
        Assert.All(authEvents, e => Assert.Equal("auth", e.Category));
    }
}
