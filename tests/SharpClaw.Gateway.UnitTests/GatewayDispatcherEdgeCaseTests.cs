using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SharpClaw.Gateway;
using SharpClaw.Gateway.Events;
using SharpClaw.Protocol.Contracts;
using SharpClaw.TestCommon;
using Xunit.Abstractions;

namespace SharpClaw.Gateway.UnitTests;

/// <summary>
/// Edge case and comprehensive tests for GatewayDispatcher.
/// </summary>
public class GatewayDispatcherEdgeCaseTests
{
    private readonly ITestOutputHelper _output;

    public GatewayDispatcherEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static GatewayDispatcher CreateDispatcher(out FakeEventPublisher publisher)
    {
        publisher = new FakeEventPublisher();
        return new GatewayDispatcher(publisher, NullLogger<GatewayDispatcher>.Instance);
    }

    #region Scope Enforcement Tests

    [Fact]
    public async Task DispatchAsync_WithRequiredScopes_DeviceHasScopes_CallsHandler()
    {
        var dispatcher = CreateDispatcher(out _);
        var handlerCalled = false;

        dispatcher.Register(
            "admin.operation",
            new[] { "operator:admin" }.ToFrozenSet(),
            (request, _, _) =>
            {
                handlerCalled = true;
                return Task.FromResult(new ResponseFrame(request.Id, true));
            });

        var deviceContext = new DeviceContext("device-1", new[] { "operator:read", "operator:admin" }.ToFrozenSet(), true);
        var response = await dispatcher.DispatchAsync(new RequestFrame("id-1", "admin.operation"), deviceContext);

        Assert.True(response.Ok);
        Assert.True(handlerCalled);
    }

    [Fact]
    public async Task DispatchAsync_WithRequiredScopes_DeviceMissingScopes_ReturnsScopeError()
    {
        var dispatcher = CreateDispatcher(out _);
        var handlerCalled = false;

        dispatcher.Register(
            "admin.operation",
            new[] { "operator:admin", "operator:write" }.ToFrozenSet(),
            (request, _, _) =>
            {
                handlerCalled = true;
                return Task.FromResult(new ResponseFrame(request.Id, true));
            });

        var deviceContext = new DeviceContext("device-1", new[] { "operator:read" }.ToFrozenSet(), true);
        var response = await dispatcher.DispatchAsync(new RequestFrame("id-1", "admin.operation"), deviceContext);

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.Unavailable, response.Error!.Code);
        Assert.Contains("operator:admin", response.Error.Message);
        Assert.Contains("operator:write", response.Error.Message);
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task DispatchAsync_ScopeCheck_IsCaseInsensitive()
    {
        var dispatcher = CreateDispatcher(out _);

        dispatcher.Register(
            "test.method",
            new[] { "OPERATOR:WRITE" }.ToFrozenSet(),
            (request, _, _) => Task.FromResult(new ResponseFrame(request.Id, true)));

        var deviceContext = new DeviceContext("device-1", new[] { "operator:write" }.ToFrozenSet(), true);
        var response = await dispatcher.DispatchAsync(new RequestFrame("id-1", "test.method"), deviceContext);

        Assert.True(response.Ok);
    }

    [Fact]
    public async Task DispatchAsync_NoRequiredScopes_AnyDeviceCanCall()
    {
        var dispatcher = CreateDispatcher(out _);

        dispatcher.Register(
            "public.method",
            FrozenSet<string>.Empty,
            (request, _, _) => Task.FromResult(new ResponseFrame(request.Id, true)));

        var deviceContext = new DeviceContext("device-1", FrozenSet<string>.Empty, true);
        var response = await dispatcher.DispatchAsync(new RequestFrame("id-1", "public.method"), deviceContext);

        Assert.True(response.Ok);
    }

    #endregion

    #region Event Subscription Tests

    [Fact]
    public async Task SubscribeConnectionAsync_ValidTopic_ReturnsTrue()
    {
        var dispatcher = CreateDispatcher(out _);

        var result = await dispatcher.SubscribeConnectionAsync("conn-1", "events.test");

        Assert.True(result);
    }

    [Fact]
    public async Task SubscribeConnectionAsync_DuplicateSubscription_ReturnsFalse()
    {
        var dispatcher = CreateDispatcher(out _);

        var first = await dispatcher.SubscribeConnectionAsync("conn-1", "events.test");
        var second = await dispatcher.SubscribeConnectionAsync("conn-1", "events.test");

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task SubscribeConnectionAsync_SameConnectionDifferentTopics_BothSucceed()
    {
        var dispatcher = CreateDispatcher(out _);

        var topic1 = await dispatcher.SubscribeConnectionAsync("conn-1", "events.topic1");
        var topic2 = await dispatcher.SubscribeConnectionAsync("conn-1", "events.topic2");

        Assert.True(topic1);
        Assert.True(topic2);
    }

    [Fact]
    public async Task UnsubscribeConnection_ExistingTopic_ReturnsTrue()
    {
        var dispatcher = CreateDispatcher(out _);

        await dispatcher.SubscribeConnectionAsync("conn-1", "events.test");
        var result = dispatcher.UnsubscribeConnection("conn-1", "events.test");

        Assert.True(result);
    }

    [Fact]
    public void UnsubscribeConnection_NonExistingTopic_ReturnsFalse()
    {
        var dispatcher = CreateDispatcher(out _);

        var result = dispatcher.UnsubscribeConnection("conn-1", "events.nonexistent");

        Assert.False(result);
    }

    [Fact]
    public async Task RemoveConnectionSubscriptions_RemovesAllSubscriptions()
    {
        var dispatcher = CreateDispatcher(out _);

        await dispatcher.SubscribeConnectionAsync("conn-1", "events.topic1");
        await dispatcher.SubscribeConnectionAsync("conn-1", "events.topic2");
        await dispatcher.SubscribeConnectionAsync("conn-1", "events.topic3");

        dispatcher.RemoveConnectionSubscriptions("conn-1");

        var subscriptions = dispatcher.GetConnectionSubscriptions("conn-1");
        Assert.Empty(subscriptions);
    }

    [Fact]
    public async Task GetConnectionSubscriptions_ReturnsSubscribedTopics()
    {
        var dispatcher = CreateDispatcher(out _);

        await dispatcher.SubscribeConnectionAsync("conn-1", "events.topic1");
        await dispatcher.SubscribeConnectionAsync("conn-1", "events.topic2");

        var subscriptions = dispatcher.GetConnectionSubscriptions("conn-1");

        Assert.Equal(2, subscriptions.Count);
        Assert.Contains("events.topic1", subscriptions);
        Assert.Contains("events.topic2", subscriptions);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var dispatcher = CreateDispatcher(out _);

        dispatcher.Dispose();
        dispatcher.Dispose(); // Should not throw

        Assert.True(true);
    }

    [Fact]
    public async Task DispatchAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var dispatcher = CreateDispatcher(out _);
        dispatcher.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await dispatcher.DispatchAsync(
                new RequestFrame("id-1", "test"),
                new DeviceContext("device-1", FrozenSet<string>.Empty, true));
        });
    }

    [Fact]
    public async Task SubscribeConnectionAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var dispatcher = CreateDispatcher(out _);
        dispatcher.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await dispatcher.SubscribeConnectionAsync("conn-1", "events.test");
        });
    }

    #endregion

    #region Argument Validation Tests

    [Fact]
    public void Register_NullMethod_ThrowsArgumentException()
    {
        var dispatcher = CreateDispatcher(out _);

        Assert.Throws<ArgumentException>(() =>
            dispatcher.Register(null!, FrozenSet<string>.Empty, (request, _, _) => Task.FromResult(new ResponseFrame(request.Id, true))));
    }

    [Fact]
    public void Register_EmptyMethod_ThrowsArgumentException()
    {
        var dispatcher = CreateDispatcher(out _);

        Assert.Throws<ArgumentException>(() =>
            dispatcher.Register("", FrozenSet<string>.Empty, (request, _, _) => Task.FromResult(new ResponseFrame(request.Id, true))));
    }

    [Fact]
    public void Register_NullHandler_ThrowsArgumentNullException()
    {
        var dispatcher = CreateDispatcher(out _);

        Assert.Throws<ArgumentNullException>(() =>
            dispatcher.Register("test.method", FrozenSet<string>.Empty, null!));
    }

    [Fact]
    public async Task DispatchAsync_NullRequest_ThrowsArgumentNullException()
    {
        var dispatcher = CreateDispatcher(out _);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await dispatcher.DispatchAsync(null!, new DeviceContext("device-1", FrozenSet<string>.Empty, true));
        });
    }

    [Fact]
    public async Task DispatchAsync_NullDeviceContext_ThrowsArgumentNullException()
    {
        var dispatcher = CreateDispatcher(out _);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await dispatcher.DispatchAsync(new RequestFrame("id-1", "test"), null!);
        });
    }

    [Fact]
    public async Task SubscribeConnectionAsync_EmptyConnectionId_ThrowsArgumentException()
    {
        var dispatcher = CreateDispatcher(out _);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await dispatcher.SubscribeConnectionAsync("", "events.test");
        });
    }

    [Fact]
    public async Task SubscribeConnectionAsync_EmptyTopic_ThrowsArgumentException()
    {
        var dispatcher = CreateDispatcher(out _);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await dispatcher.SubscribeConnectionAsync("conn-1", "");
        });
    }

    #endregion

    #region Handler Exception Tests

    [Fact]
    public async Task DispatchAsync_HandlerThrowsSpecificException_ReturnsInternalError()
    {
        var dispatcher = CreateDispatcher(out _);
        var exceptionMessage = "Custom business logic error";

        dispatcher.Register(
            "failing.method",
            FrozenSet<string>.Empty,
            (request, _, _) => throw new InvalidOperationException(exceptionMessage));

        var response = await dispatcher.DispatchAsync(
            new RequestFrame("id-1", "failing.method"),
            new DeviceContext("device-1", FrozenSet<string>.Empty, true));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.InternalError, response.Error!.Code);
    }

    [Fact]
    public async Task DispatchAsync_HandlerThrowsTimeoutException_ReturnsInternalError()
    {
        var dispatcher = CreateDispatcher(out _);

        dispatcher.Register(
            "timeout.method",
            FrozenSet<string>.Empty,
            (request, _, _) => throw new TimeoutException("Operation timed out"));

        var response = await dispatcher.DispatchAsync(
            new RequestFrame("id-1", "timeout.method"),
            new DeviceContext("device-1", FrozenSet<string>.Empty, true));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal(ErrorCodes.InternalError, response.Error!.Code);
    }

    [Fact]
    public async Task DispatchAsync_Cancellation_PropagatesCancellation()
    {
        var dispatcher = CreateDispatcher(out _);
        using var cts = new CancellationTokenSource();

        dispatcher.Register(
            "slow.method",
            FrozenSet<string>.Empty,
            async (request, _, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return new ResponseFrame(request.Id, true);
            });

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await dispatcher.DispatchAsync(
                new RequestFrame("id-1", "slow.method"),
                new DeviceContext("device-1", FrozenSet<string>.Empty, true),
                cts.Token);
        });
    }

    #endregion

    #region Event Publishing Tests

    [Fact]
    public async Task PublishEventAsync_ValidEvent_PublishesToPublisher()
    {
        var dispatcher = CreateDispatcher(out var publisher);
        var eventFrame = new EventFrame("test.event", "test data", 1);

        await dispatcher.PublishEventAsync("events.test", eventFrame);

        var published = publisher.GetPublishedEvents("events.test");
        Assert.Single(published);
        Assert.Equal("test.event", published[0].EventFrame.Event);
    }

    [Fact]
    public async Task PublishEventAsync_MultipleEvents_PreservesOrder()
    {
        var dispatcher = CreateDispatcher(out var publisher);

        for (var i = 1; i <= 5; i++)
        {
            await dispatcher.PublishEventAsync("events.test", new EventFrame($"event.{i}", null, i));
        }

        var published = publisher.GetPublishedEvents("events.test");
        Assert.Equal(5, published.Count);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal($"event.{i + 1}", published[i].EventFrame.Event);
        }
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task DispatchAsync_ConcurrentRequests_HandledSafely()
    {
        var dispatcher = CreateDispatcher(out _);
        var callCount = 0;

        dispatcher.Register(
            "concurrent.method",
            FrozenSet<string>.Empty,
            async (request, _, _) =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10);
                return new ResponseFrame(request.Id, true);
            });

        var tasks = Enumerable.Range(0, 100)
            .Select(i => dispatcher.DispatchAsync(
                new RequestFrame($"id-{i}", "concurrent.method"),
                new DeviceContext($"device-{i}", FrozenSet<string>.Empty, true)))
            .ToList();

        await Task.WhenAll(tasks);

        Assert.Equal(100, callCount);
        Assert.All(tasks, t => Assert.True(t.Result.Ok));
    }

    [Fact]
    public async Task Register_ConcurrentRegistrations_LastOneWinsOrThrows()
    {
        var dispatcher = CreateDispatcher(out _);

        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(() =>
            {
                try
                {
                    dispatcher.Register(
                        "concurrent.register",
                        FrozenSet<string>.Empty,
                        (request, _, _) => Task.FromResult(new ResponseFrame(request.Id, true, new { index = i })));
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(9, results.Count(r => !r));
    }

    #endregion
}
