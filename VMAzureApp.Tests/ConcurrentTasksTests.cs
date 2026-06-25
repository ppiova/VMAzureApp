using VMAzureApp.Services;
using Xunit;

namespace VMAzureApp.Tests;

public class ConcurrentTasksTests
{
    [Fact]
    public async Task MapAsync_ReturnsResultsInSourceOrder()
    {
        int[] source = Enumerable.Range(1, 50).ToArray();

        IReadOnlyList<int> results = await ConcurrentTasks.MapAsync(
            source,
            maxConcurrency: 8,
            async (value, _) =>
            {
                await Task.Yield();
                return value * 2;
            });

        Assert.Equal(source.Select(v => v * 2), results);
    }

    [Fact]
    public async Task MapAsync_ReturnsEmpty_ForEmptySource()
    {
        IReadOnlyList<int> results = await ConcurrentTasks.MapAsync(
            Array.Empty<int>(),
            maxConcurrency: 4,
            (value, _) => Task.FromResult(value));

        Assert.Empty(results);
    }

    [Fact]
    public async Task MapAsync_NeverExceedsMaxConcurrency()
    {
        const int maxConcurrency = 4;
        int running = 0;
        int observedMax = 0;
        object gate = new();

        await ConcurrentTasks.MapAsync(
            Enumerable.Range(1, 40).ToArray(),
            maxConcurrency,
            async (value, _) =>
            {
                int current = Interlocked.Increment(ref running);
                lock (gate)
                {
                    observedMax = Math.Max(observedMax, current);
                }

                await Task.Delay(15);
                Interlocked.Decrement(ref running);
                return value;
            });

        Assert.True(observedMax <= maxConcurrency, $"Observed {observedMax} concurrent tasks, expected at most {maxConcurrency}.");
        Assert.True(observedMax >= 2, "Expected the work to actually run in parallel.");
    }

    [Fact]
    public async Task MapAsync_PropagatesExceptions()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ConcurrentTasks.MapAsync(
                Enumerable.Range(1, 10).ToArray(),
                maxConcurrency: 3,
                (value, _) => value == 5
                    ? throw new InvalidOperationException("boom")
                    : Task.FromResult(value)));
    }

    [Fact]
    public async Task MapAsync_Throws_WhenMaxConcurrencyIsInvalid()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ConcurrentTasks.MapAsync(
                new[] { 1, 2, 3 },
                maxConcurrency: 0,
                (value, _) => Task.FromResult(value)));
    }
}
