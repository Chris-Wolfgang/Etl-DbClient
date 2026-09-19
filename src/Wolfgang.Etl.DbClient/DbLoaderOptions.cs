using System;
using System.Data;
using Wolfgang.Etl.Abstractions;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Options for the <see cref="DbLoader{TRecord}"/> constructors.
/// </summary>
/// <remarks>
/// Supplied ahead of the optional transaction and logger. When the whole options object is
/// <see langword="null"/>, or an individual property is left unset, the documented defaults below
/// apply — defaults live on the property initializers here rather than in constructor bodies, so no
/// constructor can accidentally diverge from them.
/// <para>
/// The record is not generic: none of these settings depends on the record type being loaded.
/// </para>
/// <para>
/// Derives from <see cref="LoaderOptions"/>, so the settings every loader shares —
/// <see cref="LoaderOptions.ReportingInterval"/>, <see cref="LoaderOptions.SkipItemCount"/>,
/// <see cref="LoaderOptions.MaximumItemCount"/> and <see cref="LoaderOptions.ErrorPolicy"/> — are
/// configured here as well (ADR-0009 in Wolfgang.Etl.Abstractions).
/// </para>
/// </remarks>
public sealed record DbLoaderOptions : LoaderOptions
{
    /// <summary>
    /// Gets the command timeout. Defaults to <see langword="null"/>, meaning the provider's default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is negative.
    /// </exception>
    public TimeSpan? CommandTimeout
    {
        get;
        init
        {
            if (value.HasValue && value.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(value),
                    value,
                    "CommandTimeout cannot be negative. Use null to fall back to the ADO.NET default."
                );
            }

            field = value;
        }
    }



    /// <summary>
    /// Gets how the command text is interpreted. Defaults to <see cref="System.Data.CommandType.Text"/>.
    /// </summary>
    public CommandType CommandType { get; init; } = CommandType.Text;



    /// <summary>
    /// Gets a value indicating whether the loader opens and closes the connection itself.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool ManageConnection { get; init; }



    /// <summary>
    /// Gets a value indicating whether the destination schema is validated before the first write.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool ValidateSchemaOnStart { get; init; }



    /// <summary>
    /// Gets the number of rows bundled into a single multi-row INSERT. Defaults to <c>1</c>.
    /// </summary>
    /// <remarks>
    /// The underlying property rejects values below <c>1</c>, so this default is <c>1</c> rather
    /// than the type default of <c>0</c> — a zero default would make every options-constructed
    /// loader throw. Takes precedence over <see cref="BatchSize"/> when both are set above <c>1</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is less than <c>1</c>.
    /// </exception>
    public int InsertBatchSize
    {
        get;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(value),
                    value,
                    "InsertBatchSize must be at least 1."
                );
            }

            field = value;
        }
    } = 1;



    /// <summary>
    /// Gets the action taken when a row fails. Defaults to <see cref="RowErrorHandling.Abort"/>.
    /// </summary>
    public RowErrorHandling ErrorHandling { get; init; } = RowErrorHandling.Abort;



    /// <summary>
    /// Gets the number of row failures tolerated before the load aborts. Defaults to <c>0</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is negative.
    /// </exception>
    public int MaxErrorCount
    {
        get;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(value),
                    value,
                    "MaxErrorCount cannot be negative. Use 0 for unlimited."
                );
            }

            field = value;
        }
    }



    /// <summary>
    /// Gets how many rows are written between transaction commits. Defaults to <c>0</c>,
    /// meaning a single commit at the end.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is negative.
    /// </exception>
    public int BatchCommitSize
    {
        get;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(value),
                    value,
                    "BatchCommitSize cannot be negative. Use 0 for 'commit only at end'."
                );
            }

            field = value;
        }
    }



    /// <summary>
    /// Gets the number of records buffered per round trip. Defaults to <c>1</c>.
    /// </summary>
    /// <remarks>
    /// As with <see cref="InsertBatchSize"/>, the underlying property rejects values below <c>1</c>,
    /// so the default here is <c>1</c> rather than the type default of <c>0</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than 1.
    /// </exception>
    public int BatchSize
    {
        get;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException
                (
                    nameof(value),
                    value,
                    "BatchSize must be at least 1."
                );
            }

            field = value;
        }
    } = 1;



    /// <summary>
    /// Gets a value indicating whether the loader runs without writing to the database. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Applied to <see cref="DbLoader{TRecord}.IsDryRun"/>: rows are consumed and counted and batches
    /// are formed, but no command is executed against the destination table.
    /// </remarks>
    public bool IsDryRun { get; init; }
}
