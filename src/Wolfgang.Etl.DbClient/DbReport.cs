using Wolfgang.Etl.Abstractions;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Progress report for ADO.NET extraction and loading operations.
/// Extends the base <see cref="Report"/> with database-specific context.
/// </summary>
public record DbReport : Report
{
    /// <summary>
    /// Initializes a new <see cref="DbReport"/> snapshot.
    /// </summary>
    /// <param name="currentItemCount">The number of records processed so far.</param>
    /// <param name="currentSkippedItemCount">The number of records skipped so far.</param>
    /// <param name="commandText">The SQL command text being executed.</param>
    /// <param name="elapsedMilliseconds">The wall clock time since execution started.</param>
    public DbReport
    (
        int currentItemCount,
        int currentSkippedItemCount,
        string commandText,
        long elapsedMilliseconds
    )
        : this(currentItemCount, currentSkippedItemCount, commandText, elapsedMilliseconds, totalItemCount: null)
    {
    }



    /// <summary>
    /// Initializes a new <see cref="DbReport"/> snapshot with a total item count.
    /// </summary>
    /// <param name="currentItemCount">The number of records processed so far.</param>
    /// <param name="currentSkippedItemCount">The number of records skipped so far.</param>
    /// <param name="commandText">The SQL command text being executed.</param>
    /// <param name="elapsedMilliseconds">The wall clock time since execution started.</param>
    /// <param name="totalItemCount">
    /// The total number of records available, or <c>null</c> if
    /// <see cref="DbExtractor{TRecord}.TotalCountQuery"/> was not set.
    /// </param>
    public DbReport
    (
        int currentItemCount,
        int currentSkippedItemCount,
        string commandText,
        long elapsedMilliseconds,
        int? totalItemCount
    )
        : base(currentItemCount)
    {
        CurrentSkippedItemCount = currentSkippedItemCount;
        CommandText = commandText;
        ElapsedMilliseconds = elapsedMilliseconds;
        TotalItemCount = totalItemCount;
    }



    /// <summary>
    /// The number of records skipped so far.
    /// </summary>
    public int CurrentSkippedItemCount { get; }



    /// <summary>
    /// The SQL command text being executed. Never null — always assigned by the constructor.
    /// </summary>
    public string CommandText { get; } = string.Empty;



    /// <summary>
    /// The wall clock time in milliseconds since the operation started.
    /// </summary>
    public long ElapsedMilliseconds { get; }



    /// <summary>
    /// The total number of records available for extraction, or <c>null</c> if
    /// <see cref="DbExtractor{TRecord}.TotalCountQuery"/> was not set.
    /// </summary>
    public int? TotalItemCount { get; }
}
