using System;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Payload for <c>DbLoader{TRecord}.RowFailed</c>. Fires once per row that
/// failed to load when <c>ErrorHandling</c> is <see cref="RowErrorHandling.Skip"/>.
/// </summary>
public sealed class RowFailedEventArgs<TRecord> : EventArgs
{
    /// <summary>
    /// Initializes a new <see cref="RowFailedEventArgs{TRecord}"/>.
    /// </summary>
    /// <param name="record">The record whose insert/update threw.</param>
    /// <param name="exception">The exception thrown by the underlying provider.</param>
    /// <param name="itemIndex">
    /// 1-based index of the row in the source enumerable, counted after
    /// <c>SkipItemCount</c> rows have been skipped.
    /// </param>
    public RowFailedEventArgs(TRecord record, Exception exception, long itemIndex)
    {
        Record = record;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        ItemIndex = itemIndex;
    }

    /// <summary>The record whose insert/update threw.</summary>
    public TRecord Record { get; }

    /// <summary>The exception thrown by the underlying provider.</summary>
    public Exception Exception { get; }

    /// <summary>
    /// 1-based index of the row in the source enumerable (after
    /// <c>SkipItemCount</c> rows have been skipped). Useful for dead-letter
    /// capture and ordering.
    /// </summary>
    public long ItemIndex { get; }
}
