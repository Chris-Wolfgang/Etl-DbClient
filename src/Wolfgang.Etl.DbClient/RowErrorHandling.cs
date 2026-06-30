namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Controls how <see cref="DbLoader{TRecord}"/> reacts when a single row's
/// <c>ExecuteAsync</c> call throws.
/// </summary>
public enum RowErrorHandling
{
    /// <summary>
    /// First failure rolls back the auto-managed transaction (if any) and
    /// propagates the exception out of the load. This is the default and
    /// matches the v0.4.0 behavior.
    /// </summary>
    Abort,

    /// <summary>
    /// Log/event the failure via <c>DbLoader.RowFailed</c>, advance
    /// <c>CurrentErrorCount</c>, and continue processing the next row. If
    /// <c>DbLoader.MaxErrorCount</c> is non-zero and the error count reaches
    /// it, the load aborts with an <see cref="System.InvalidOperationException"/>
    /// referencing the threshold and the last underlying exception is included
    /// as the inner exception.
    /// </summary>
    /// <remarks>
    /// Skip mode only applies on the per-record path (<c>BatchSize == 1</c>,
    /// the default). When <c>BatchSize &gt; 1</c>, a failed batch can't be
    /// reliably attributed to a single row without per-item retry, so the
    /// load aborts even in <see cref="Skip"/> mode. Set <c>BatchSize = 1</c>
    /// if row-level error handling is required.
    /// </remarks>
    Skip,
}
