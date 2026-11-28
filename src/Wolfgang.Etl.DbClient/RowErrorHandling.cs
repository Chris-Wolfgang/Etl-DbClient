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
    /// Skip mode only applies on the per-record path (<c>BatchSize == 1</c>
    /// and <c>InsertBatchSize == 1</c>, both the default). When either is
    /// greater than <c>1</c>, a failed batch can't be reliably attributed to a
    /// single row without per-item retry, so the load aborts even in
    /// <see cref="Skip"/> mode. Leave both at <c>1</c> if row-level error
    /// handling is required.
    /// <para>
    /// A failure that leaves the connection no longer open (a dropped connection, a fatal
    /// provider error) is not treated as a row failure: it aborts the load instead of being
    /// skipped once per remaining row.
    /// </para>
    /// <para>
    /// Skip mode carries on in the same transaction, so it only isolates a bad row where the
    /// provider keeps the transaction usable after a failed statement. PostgreSQL (and, by its
    /// documented behaviour, CockroachDB) abort the whole transaction on the first failed statement:
    /// every later row then fails ("current transaction is aborted", SQLSTATE 25P02) and nothing is
    /// committed. On net5.0 and later the loader reads <c>DbException.SqlState</c>: the first
    /// 25P02 stops the load with an <see cref="System.InvalidOperationException"/> (the provider
    /// exception is the inner exception), after <c>DbLoader.RowFailed</c> has reported only the row
    /// that really failed. On older targets <c>SqlState</c> does not exist, so every later row is
    /// still reported as failed. On those providers set <c>BatchCommitSize</c> to <c>1</c> so each
    /// row runs in its own transaction (verified against PostgreSQL 16: only the bad row is skipped).
    /// </para>
    /// </remarks>
    Skip,
}
