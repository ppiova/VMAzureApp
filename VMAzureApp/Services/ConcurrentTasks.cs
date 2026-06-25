namespace VMAzureApp.Services;

/// <summary>
/// Helpers for running asynchronous work over a collection with a bounded
/// degree of parallelism. Used to fan out per-VM Azure calls without flooding
/// the ARM endpoint or running them one-at-a-time.
/// </summary>
internal static class ConcurrentTasks
{
    /// <summary>
    /// Projects each item of <paramref name="source"/> through
    /// <paramref name="mapper"/>, running at most <paramref name="maxConcurrency"/>
    /// mappers at a time. Results are returned in the same order as the source.
    /// </summary>
    public static async Task<IReadOnlyList<TResult>> MapAsync<TSource, TResult>(
        IReadOnlyList<TSource> source,
        int maxConcurrency,
        Func<TSource, CancellationToken, Task<TResult>> mapper,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);

        if (source.Count == 0)
        {
            return [];
        }

        using SemaphoreSlim throttle = new(maxConcurrency);
        TResult[] results = new TResult[source.Count];
        Task[] tasks = new Task[source.Count];

        for (int i = 0; i < source.Count; i++)
        {
            tasks[i] = RunAsync(i);
        }

        await Task.WhenAll(tasks);
        return results;

        async Task RunAsync(int index)
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                results[index] = await mapper(source[index], cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        }
    }
}
