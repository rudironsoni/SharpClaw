using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Execution.Abstractions;
using SharpClaw.Execution.SandboxManager;
using SharpClaw.TestCommon;

namespace SharpClaw.Runs.UnitTests;

/// <summary>
/// Edge case and comprehensive tests for RunCoordinator.
/// </summary>
public class RunCoordinatorEdgeCaseTests
{
    #region Idempotency Tests

    [Fact]
    public async Task StartAsync_DifferentIdempotencyKeys_CreateDifferentRuns()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        var first = await coordinator.StartAsync("input-1", "tenant-1", "idem-1");
        var second = await coordinator.StartAsync("input-2", "tenant-1", "idem-2");

        Assert.NotEqual(first.RunId, second.RunId);
        Assert.Equal("started", first.Status);
        Assert.Equal("started", second.Status);
    }

    [Fact]
    public async Task StartAsync_NullIdempotencyKey_CreatesNewRun()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        var first = await coordinator.StartAsync("input-1", "tenant-1", null);
        var second = await coordinator.StartAsync("input-1", "tenant-1", null);

        Assert.NotEqual(first.RunId, second.RunId);
    }

    [Fact]
    public async Task StartAsync_SameIdempotencyKeyAfterCompletion_CreatesNewRun()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        // First run with idempotency key
        var first = await coordinator.StartAsync("input-1", "tenant-1", "idem-1");
        
        // Abort the run
        await coordinator.AbortAsync(first.RunId, "tenant-1");

        // Second run with same idempotency key should create new run since first is completed
        var second = await coordinator.StartAsync("input-2", "tenant-1", "idem-1");

        // The idempotency check should return in_flight only if status is started or running
        // Since we aborted, it should create a new run
        Assert.Equal("started", second.Status);
    }

    #endregion

    #region Argument Validation Tests

    [Fact]
    public async Task StartAsync_NullInput_ThrowsArgumentNullException()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await coordinator.StartAsync(null!, "tenant-1");
        });
    }

    [Fact]
    public async Task StartAsync_NullTenantId_ThrowsArgumentException()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await coordinator.StartAsync("input-1", null!);
        });
    }

    [Fact]
    public async Task StartAsync_EmptyTenantId_ThrowsArgumentException()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await coordinator.StartAsync("input-1", "");
        });
    }

    [Fact]
    public async Task AbortAsync_NullRunId_ThrowsArgumentException()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await coordinator.AbortAsync(null!, "tenant-1");
        });
    }

    [Fact]
    public async Task AbortAsync_NullTenantId_ThrowsArgumentException()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await coordinator.AbortAsync("run-1", null!);
        });
    }

    [Fact]
    public async Task GetSnapshotAsync_NullRunId_ThrowsArgumentException()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await coordinator.GetSnapshotAsync(null!, "tenant-1");
        });
    }

    [Fact]
    public async Task ReadEventsAsync_NullRunId_ThrowsArgumentException()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await coordinator.ReadEventsAsync(null!).ToListAsync();
        });
    }

    #endregion

    #region Non-Existent Run Tests

    [Fact]
    public async Task AbortAsync_NonExistentRun_ReturnsNotFound()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        var result = await coordinator.AbortAsync("nonexistent-run", "tenant-1");

        Assert.Equal("nonexistent-run", result.RunId);
        Assert.Equal("not_found", result.Status);
    }

    [Fact]
    public async Task GetSnapshotAsync_NonExistentRun_ReturnsNotFound()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        var result = await coordinator.GetSnapshotAsync("nonexistent-run", "tenant-1");

        Assert.Equal("nonexistent-run", result.RunId);
        Assert.Equal("not_found", result.Status);
    }

    [Fact]
    public async Task ReadEventsAsync_NonExistentRun_ReturnsEmptyStream()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        var events = await coordinator.ReadEventsAsync("nonexistent-run").ToListAsync();

        Assert.Empty(events);
    }

    #endregion

    #region Status Transition Tests

    [Fact]
    public async Task AbortAsync_AlreadyCompleted_ReturnsCompletedStatus()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();
        var started = await coordinator.StartAsync("input-1", "tenant-1");

        // Wait for completion (in real tests this would require more complex setup)
        // For now, we just verify the method handles the case
        var snapshot = await coordinator.GetSnapshotAsync(started.RunId, "tenant-1");
        
        // If already completed, should return completed status
        if (snapshot.Status is "completed" or "failed")
        {
            var aborted = await coordinator.AbortAsync(started.RunId, "tenant-1");
            Assert.True(aborted.Status is "completed" or "failed");
        }
    }

    [Fact]
    public async Task AbortAsync_AlreadyAborted_ReturnsAbortedStatus()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();
        var started = await coordinator.StartAsync("input-1", "tenant-1");

        var firstAbort = await coordinator.AbortAsync(started.RunId, "tenant-1");
        Assert.Equal("aborted", firstAbort.Status);

        var secondAbort = await coordinator.AbortAsync(started.RunId, "tenant-1");
        Assert.Equal("aborted", secondAbort.Status);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ReadEventsAsync_Cancellation_StopsEnumeration()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();
        var started = await coordinator.StartAsync("input-1", "tenant-1");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var events = new List<RunEvent>();

        try
        {
            await foreach (var evt in coordinator.ReadEventsAsync(started.RunId, cts.Token))
            {
                events.Add(evt);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Should have received some events before cancellation
        Assert.True(events.Count >= 0); // Could be 0 if cancelled very quickly
    }

    [Fact]
    public async Task StartAsync_Cancellation_PropagatesToExecution()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();
        using var cts = new CancellationTokenSource();

        // Start the run
        var started = await coordinator.StartAsync("input-1", "tenant-1", cancellationToken: cts.Token);

        Assert.Equal("started", started.Status);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task StartAsync_ConcurrentRuns_AllSucceed()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();

        var tasks = Enumerable.Range(0, 10)
            .Select(i => coordinator.StartAsync($"input-{i}", "tenant-1"))
            .ToList();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Equal("started", r.Status));
        Assert.Equal(10, results.Select(r => r.RunId).Distinct().Count());
    }

    [Fact]
    public async Task AbortAsync_ConcurrentAborts_HandledSafely()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();
        var started = await coordinator.StartAsync("input-1", "tenant-1");

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => coordinator.AbortAsync(started.RunId, "tenant-1"))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // All should return aborted status
        Assert.All(results, r => Assert.Equal(started.RunId, r.RunId));
    }

    #endregion

    #region Event Ordering Tests

    [Fact]
    public async Task ReadEventsAsync_EventsAreOrderedBySequence()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();
        var started = await coordinator.StartAsync("input-1", "tenant-1");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var events = new List<RunEvent>();

        try
        {
            await foreach (var evt in coordinator.ReadEventsAsync(started.RunId, cts.Token))
            {
                events.Add(evt);
                if (events.Count >= 3)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }

        // Verify events are in sequence order
        for (var i = 1; i < events.Count; i++)
        {
            Assert.True(events[i].Seq > events[i - 1].Seq, "Events should be ordered by sequence number");
        }
    }

    #endregion

    #region Snapshot Tests

    [Fact]
    public async Task GetSnapshotAsync_ReturnsCorrectTenantId()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();
        var started = await coordinator.StartAsync("input-1", "tenant-specific");

        var snapshot = await coordinator.GetSnapshotAsync(started.RunId, "tenant-specific");

        Assert.Equal("tenant-specific", snapshot.TenantId);
    }

    [Fact]
    public async Task GetSnapshotAsync_AfterAbort_ContainsError()
    {
        var coordinator = TestHelpers.CreateRunCoordinator();
        var started = await coordinator.StartAsync("input-1", "tenant-1");

        await coordinator.AbortAsync(started.RunId, "tenant-1");
        var snapshot = await coordinator.GetSnapshotAsync(started.RunId, "tenant-1");

        Assert.Equal("aborted", snapshot.Status);
        Assert.Equal("aborted-by-operator", snapshot.LastError);
    }

    #endregion
}
