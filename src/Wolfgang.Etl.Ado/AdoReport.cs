using Wolfgang.Etl.Abstractions;

namespace Wolfgang.Etl.Ado;

/// <summary>
/// Progress report for ADO.NET extraction and loading operations.
/// Extends the base <see cref="Report"/> with database-specific context.
/// </summary>
public record AdoReport : Report
{
    /// <summary>
    /// Initializes a new <see cref="AdoReport"/> snapshot.
    /// </summary>
    /// <param name="currentItemCount">The number of records processed so far.</param>
    /// <param name="currentSkippedItemCount">The number of records skipped so far.</param>
    /// <param name="commandText">The SQL command text being executed.</param>
    /// <param name="elapsedMilliseconds">The wall clock time since execution started.</param>
    public AdoReport
    (
        int currentItemCount,
        int currentSkippedItemCount,
        string commandText,
        long elapsedMilliseconds
    )
        : base(currentItemCount)
    {
        CurrentSkippedItemCount = currentSkippedItemCount;
        CommandText = commandText;
        ElapsedMilliseconds = elapsedMilliseconds;
    }



    /// <summary>
    /// The number of records skipped so far.
    /// </summary>
    public int CurrentSkippedItemCount { get; }



    /// <summary>
    /// The SQL command text being executed.
    /// </summary>
    public string CommandText { get; }



    /// <summary>
    /// The wall clock time in milliseconds since the operation started.
    /// </summary>
    public long ElapsedMilliseconds { get; }
}
