using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SharpClaw.TestCommon;

/// <summary>
/// Helper methods for waiting on async conditions in tests.
/// </summary>
public static class WaitHelpers
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Waits until the specified condition returns true or the timeout is reached.
    /// </summary>
    public static async Task WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        string? message = null)
    {
        var actualTimeout = timeout ?? DefaultTimeout;
        var actualPollInterval = pollInterval ?? DefaultPollInterval;
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < actualTimeout)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(actualPollInterval);
        }

        throw new TimeoutException(message ?? $"Condition was not met within {actualTimeout}");
    }

    /// <summary>
    /// Waits until the specified condition returns true or the timeout is reached (synchronous version).
    /// </summary>
    public static void WaitUntil(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        string? message = null)
    {
        var actualTimeout = timeout ?? DefaultTimeout;
        var actualPollInterval = pollInterval ?? DefaultPollInterval;
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < actualTimeout)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(actualPollInterval);
        }

        throw new TimeoutException(message ?? $"Condition was not met within {actualTimeout}");
    }

    /// <summary>
    /// Waits for an async enumerable to produce at least N items.
    /// </summary>
    public static async IAsyncEnumerable<T> TakeWithTimeoutAsync<T>(
        IAsyncEnumerable<T> source,
        int count,
        TimeSpan? timeout = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var actualTimeout = timeout ?? DefaultTimeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(actualTimeout);

        var collected = 0;
        await foreach (var item in source.WithCancellation(cts.Token))
        {
            yield return item;
            if (++collected >= count)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Retries an async operation with exponential backoff.
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        TimeSpan? initialDelay = null)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(100);
        Exception? lastException = null;

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (i < maxRetries - 1)
                {
                    await Task.Delay(delay);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                }
            }
        }

        throw new InvalidOperationException($"Operation failed after {maxRetries} retries", lastException);
    }
}
