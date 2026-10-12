namespace Wolfgang.Etl.DbClient.Tests.Concurrency;

/// <summary>
/// Straight-line capture of how a racing operation ended.
/// </summary>
/// <remarks>
/// The cancellation-race tests must not branch on which side of the race won:
/// a <c>catch</c> or loop body that only runs when one side wins leaves lines
/// whose coverage depends on thread-pool timing. These helpers absorb the
/// outcome into a value the test then asserts on, and the deterministic
/// companion facts in each test class drive both outcomes on every run.
/// </remarks>
internal static class Outcome
{
    /// <summary>
    /// Enumerates <paramref name="source"/> to the end, returning how many items
    /// were observed and the exception that ended the enumeration, if any.
    /// </summary>
    public static async Task<(int Observed, Exception? Fault)> DrainAsync<T>(IAsyncEnumerable<T> source)
    {
        var observed = 0;
        try
        {
            await foreach (var _ in source.ConfigureAwait(false))
            {
                observed++;
            }

            return (observed, null);
        }
        catch (Exception ex)
        {
            return (observed, ex);
        }
    }



    /// <summary>
    /// Awaits <paramref name="operation"/>, returning the exception that ended it,
    /// or <see langword="null"/> when it completed normally.
    /// </summary>
    public static async Task<Exception?> FaultOfAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
