using SharpClaw.Runs;

namespace SharpClaw.Runs.UnitTests;

public class RunCoordinatorUnitTests
{
    [Fact]
    public async Task StartAsync_WithSameIdempotencyKey_ReturnsInFlightWithSameRunId()
    {
        var coordinator = new RunCoordinator();

        var first = await coordinator.StartAsync("hello", "idem-1");
        var second = await coordinator.StartAsync("hello", "idem-1");

        Assert.Equal("started", first.Status);
        Assert.Equal("in_flight", second.Status);
        Assert.Equal(first.RunId, second.RunId);
    }

    [Fact]
    public async Task AbortAsync_TransitionsRunToAborted()
    {
        var coordinator = new RunCoordinator();
        var started = await coordinator.StartAsync("hello", "idem-2");

        var aborted = await coordinator.AbortAsync(started.RunId);

        Assert.Equal(started.RunId, aborted.RunId);
        Assert.Equal("aborted", aborted.Status);
    }

    [Fact]
    public async Task ReadEventsAsync_EmitsStartedThenRunning()
    {
        var coordinator = new RunCoordinator();
        var started = await coordinator.StartAsync("hello", "idem-3");

        var events = new List<RunEvent>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await foreach (var evt in coordinator.ReadEventsAsync(started.RunId, cts.Token))
        {
            events.Add(evt);
            if (events.Count >= 2)
            {
                break;
            }
        }

        Assert.True(events.Count >= 2);
        Assert.Equal("run.started", events[0].Event);
        Assert.Equal("run.running", events[1].Event);
    }
}
