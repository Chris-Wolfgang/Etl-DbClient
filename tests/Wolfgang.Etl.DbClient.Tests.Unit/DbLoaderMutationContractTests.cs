using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Fourth batch of mutation-survivor tests for #474, all on <see cref="DbLoader{TRecord}"/>:
/// the deprecated setters' messages and boundaries, the options constructors, the multi-row
/// INSERT path's skip, maximum, flush points and error messages, the batched flush points, the
/// skip-mode failure index, and the loader's log messages.
/// </summary>
/// <remarks>
/// A Stryker run scoped to <c>DbLoader.cs</c> on top of batch 3 left 102 survivors and 14
/// uncovered mutants. The existing tests assert final row counts and exception types, so
/// mutations that change messages, flush timing or the skip/maximum bookkeeping on the
/// multi-row path survived. <c>ConfigureAwait(false)</c> boolean mutations are equivalent and
/// are not targeted.
/// </remarks>
public class DbLoaderMutationContractTests
{
    private const string InsertContractItem =
        "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)";

    private const string FailingInsert =
        "INSERT INTO ContractItems (Name, no_such_column) VALUES (@Name, @Value)";



    [ExcludeFromCodeCoverage]
    private sealed class NullConnectionFactory : DbProviderFactory
    {
        public override DbConnection? CreateConnection() => null;
    }



    private static ContractRecord[] CreateItems(int count)
    {
        var items = new ContractRecord[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = new ContractRecord { Name = $"Item{i + 1}", Value = (i + 1) * 10 };
        }
        return items;
    }



    private static async Task<List<int>> ReadValuesAsync(DbConnection connection)
    {
        var values = new List<int>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM ContractItems ORDER BY rowid";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetInt32(0));
        }
        return values;
    }



    /// <summary>
    /// Yields <paramref name="items"/>, recording the loader's <c>CurrentItemCount</c> just
    /// before each one is handed over, so a test can see exactly when each flush happened.
    /// </summary>
    private static async IAsyncEnumerable<ContractRecord> ObserveFlushesAsync
    (
        IEnumerable<ContractRecord> items,
        Func<int> currentItemCount,
        List<int> snapshots,
        [EnumeratorCancellation] CancellationToken token = default
    )
    {
        foreach (var item in items)
        {
            token.ThrowIfCancellationRequested();
            snapshots.Add(currentItemCount());
            await Task.Yield();
            yield return item;
        }
    }



    // ------------------------------------------------------------------
    // Deprecated setters: exact messages and boundaries
    // ------------------------------------------------------------------

#pragma warning disable CS0618 // The deprecated setters are the code under test.

    [Fact]
    public void CommandTimeout_setter_when_negative_message_is_exact()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(conn, InsertContractItem, options: null);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => loader.CommandTimeout = TimeSpan.FromSeconds(-1));

        Assert.StartsWith
        (
            "CommandTimeout cannot be negative. Use null to fall back to the ADO.NET default.",
            ex.Message,
            StringComparison.Ordinal
        );
    }



    [Fact]
    public void CommandTimeout_setter_when_zero_is_accepted()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(conn, InsertContractItem, options: null);

        loader.CommandTimeout = TimeSpan.Zero;

        Assert.Equal(TimeSpan.Zero, loader.CommandTimeout);
    }



    [Fact]
    public void InsertBatchSize_setter_when_zero_message_is_exact()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(conn, InsertContractItem, options: null);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => loader.InsertBatchSize = 0);

        Assert.StartsWith("InsertBatchSize must be at least 1.", ex.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void MaxErrorCount_setter_when_negative_message_is_exact()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(conn, InsertContractItem, options: null);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => loader.MaxErrorCount = -1);

        Assert.StartsWith("MaxErrorCount cannot be negative. Use 0 for unlimited.", ex.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void MaxErrorCount_setter_when_zero_is_accepted()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(conn, InsertContractItem, options: null);

        loader.MaxErrorCount = 5;
        loader.MaxErrorCount = 0;

        Assert.Equal(0, loader.MaxErrorCount);
    }



    [Fact]
    public void BatchCommitSize_setter_when_negative_message_is_exact()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(conn, InsertContractItem, options: null);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => loader.BatchCommitSize = -1);

        Assert.StartsWith
        (
            "BatchCommitSize cannot be negative. Use 0 for 'commit only at end'.",
            ex.Message,
            StringComparison.Ordinal
        );
    }



    [Fact]
    public void BatchCommitSize_setter_when_zero_is_accepted()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(conn, InsertContractItem, options: null);

        loader.BatchCommitSize = 5;
        loader.BatchCommitSize = 0;

        Assert.Equal(0, loader.BatchCommitSize);
    }



    [Fact]
    public void BatchSize_setter_when_zero_message_is_exact()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(conn, InsertContractItem, options: null);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => loader.BatchSize = 0);

        Assert.StartsWith("BatchSize must be at least 1.", ex.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void BatchSize_setter_when_one_is_accepted()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(conn, InsertContractItem, options: null);

        loader.BatchSize = 5;
        loader.BatchSize = 1;

        Assert.Equal(1, loader.BatchSize);
    }

#pragma warning restore CS0618



    // ------------------------------------------------------------------
    // Options constructors
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_with_a_WriteMode_applies_the_options()
    {
        using var conn = TestDb.CreateConnection();

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            WriteMode.Insert,
            new DbLoaderOptions { BatchSize = 3, MaxErrorCount = 2, IsDryRun = true }
        );

        Assert.Equal(3, loader.BatchSize);
        Assert.Equal(2, loader.MaxErrorCount);
        Assert.True(loader.IsDryRun);
    }



    [Fact]
    public void Constructor_with_a_provider_factory_applies_the_options()
    {
        var loader = new DbLoader<ContractRecord>
        (
            SqliteFactory.Instance,
            "Data Source=:memory:",
            InsertContractItem,
            new DbLoaderOptions { InsertBatchSize = 4, BatchCommitSize = 7, ManageConnection = true }
        );

        Assert.Equal(4, loader.InsertBatchSize);
        Assert.Equal(7, loader.BatchCommitSize);
        Assert.True(loader.ManageConnection);
    }



    [Fact]
    public void Constructor_when_factory_returns_no_connection_message_is_exact()
    {
        var ex = Assert.Throws<InvalidOperationException>
        (
            () => new DbLoader<ContractRecord>(new NullConnectionFactory(), "Data Source=:memory:", InsertContractItem, options: null)
        );

        Assert.Equal
        (
            $"{typeof(NullConnectionFactory).FullName}.CreateConnection() returned null. " +
            "The provider factory does not produce DbConnection instances.",
            ex.Message
        );
    }



    // ------------------------------------------------------------------
    // Multi-row INSERT (InsertBatchSize > 1)
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_honors_SkipItemCount_and_MaximumItemCount()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            InsertContractItem,
            new DbLoaderOptions { InsertBatchSize = 2, SkipItemCount = 1, MaximumItemCount = 3 }
        );

        await loader.LoadAsync(CreateItems(6).ToAsyncEnumerable());

        Assert.Equal
        (
            new[] { 20, 30, 40 },
            await ReadValuesAsync(conn)
        );
        Assert.Equal(3, loader.CurrentItemCount);
        Assert.Equal(1, loader.CurrentSkippedItemCount);
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_flushes_each_full_chunk_before_reading_on()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            InsertContractItem,
            new DbLoaderOptions { InsertBatchSize = 2 }
        );
        var snapshots = new List<int>();

        await loader.LoadAsync(ObserveFlushesAsync(CreateItems(5), () => loader.CurrentItemCount, snapshots));

        // A chunk is written as soon as it holds InsertBatchSize rows; the trailing row is
        // written once the source ends.
        Assert.Equal
        (
            new[] { 0, 0, 2, 2, 4 },
            snapshots
        );
        Assert.Equal(5, loader.CurrentItemCount);
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_logs_Debug_for_skipped_loaded_and_maximum()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            InsertContractItem,
            new DbLoaderOptions { InsertBatchSize = 2, SkipItemCount = 1, MaximumItemCount = 2 },
            logger: logger
        );

        await loader.LoadAsync(CreateItems(5).ToAsyncEnumerable());

        var debug = logger.Entries.Where(e => e.Level == LogLevel.Debug).Select(e => e.Message).ToList();
        Assert.Contains("Skipping item (1/1)", debug);
        Assert.Contains("Loaded record (item #1)", debug);
        Assert.Contains("Loaded record (item #2)", debug);
        Assert.Contains("MaximumItemCount (2) reached, stopping loading", debug);
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_and_IsDryRun_logs_Debug_per_record()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            InsertContractItem,
            new DbLoaderOptions { InsertBatchSize = 2, IsDryRun = true },
            logger: logger
        );

        await loader.LoadAsync(CreateItems(3).ToAsyncEnumerable());

        Assert.Equal
        (
            3,
            logger.Entries.Count(e => e.Level == LogLevel.Debug && e.Message.StartsWith("Loaded record", StringComparison.Ordinal))
        );
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_CommandText_has_no_VALUES_message_is_exact()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        const string commandText = "UPDATE ContractItems SET Value = @Value WHERE Name = @Name";
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            commandText,
            new DbLoaderOptions { InsertBatchSize = 2 }
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(CreateItems(2).ToAsyncEnumerable())
        );

        Assert.Equal
        (
            "InsertBatchSize > 1 requires CommandText to contain 'VALUES (template)'. " +
            $"CommandText was: {commandText}",
            ex.Message
        );
    }



    [Theory]
    [InlineData("(@Name, @Value")]
    [InlineData("@Name, @Value)")]
    [InlineData("(")]
    public async Task LoadAsync_with_InsertBatchSize_when_VALUES_clause_is_not_one_parenthesized_template_message_is_exact
    (
        string valuesClause
    )
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES " + valuesClause,
            new DbLoaderOptions { InsertBatchSize = 2 }
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(CreateItems(2).ToAsyncEnumerable())
        );

        Assert.Equal
        (
            "InsertBatchSize > 1 requires the VALUES clause to be a single parenthesized " +
            "parameter template such as '(@A, @B, @C)'. Got: " + valuesClause,
            ex.Message
        );
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_a_parameter_has_no_property_message_is_exact()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @NoSuchProperty)",
            new DbLoaderOptions { InsertBatchSize = 2 }
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(CreateItems(2).ToAsyncEnumerable())
        );

        Assert.Equal
        (
            "InsertBatchSize > 1: TRecord 'ContractRecord' has no public property matching parameter '@NoSuchProperty'.",
            ex.Message
        );
    }



    // ------------------------------------------------------------------
    // Batched execution (BatchSize > 1)
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_with_BatchSize_flushes_each_full_batch_before_reading_on()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            InsertContractItem,
            new DbLoaderOptions { BatchSize = 2 }
        );
        var snapshots = new List<int>();

        await loader.LoadAsync(ObserveFlushesAsync(CreateItems(5), () => loader.CurrentItemCount, snapshots));

        Assert.Equal
        (
            new[] { 0, 0, 2, 2, 4 },
            snapshots
        );
        Assert.Equal
        (
            new[] { 10, 20, 30, 40, 50 },
            await ReadValuesAsync(conn)
        );
    }



    [Fact]
    public async Task LoadAsync_with_BatchSize_logs_Debug_for_skipped_loaded_and_maximum()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            InsertContractItem,
            new DbLoaderOptions { BatchSize = 2, SkipItemCount = 1, MaximumItemCount = 2 },
            logger: logger
        );

        await loader.LoadAsync(CreateItems(5).ToAsyncEnumerable());

        var debug = logger.Entries.Where(e => e.Level == LogLevel.Debug).Select(e => e.Message).ToList();
        Assert.Contains("Skipping item (1/1)", debug);
        Assert.Contains("Loaded record (item #1)", debug);
        Assert.Contains("Loaded record (item #2)", debug);
        Assert.Contains("MaximumItemCount (2) reached, stopping loading", debug);
    }



    // ------------------------------------------------------------------
    // Skip-mode failures
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_in_Skip_mode_when_MaxErrorCount_is_exceeded_message_counts_skipped_items_in_the_index()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            FailingInsert,
            new DbLoaderOptions { ErrorHandling = RowErrorHandling.Skip, MaxErrorCount = 1, SkipItemCount = 2 }
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(CreateItems(5).ToAsyncEnumerable())
        );

        // Two skipped items plus the first failure: the third item from the source.
        Assert.Equal
        (
            "DbLoader.MaxErrorCount (1) exceeded. Last failure on item index 3; see inner exception.",
            ex.Message
        );
    }



    [Fact]
    public async Task LoadAsync_in_Skip_mode_logs_a_Warning_per_failed_row()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            FailingInsert,
            new DbLoaderOptions { ErrorHandling = RowErrorHandling.Skip },
            logger: logger
        );

        await loader.LoadAsync(CreateItems(2).ToAsyncEnumerable());

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Equal(2, warnings.Count);
        Assert.Equal
        (
            "Row 2 failed and was skipped (2 errors so far). Configured ErrorHandling=Skip; MaxErrorCount=0",
            warnings[1].Message
        );
        Assert.NotNull(warnings[1].Exception);
    }



    // ------------------------------------------------------------------
    // Start and completion messages
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_start_and_completion_messages_are_exact()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>
        (
            conn,
            InsertContractItem,
            new DbLoaderOptions { SkipItemCount = 1, MaximumItemCount = 2 },
            logger: logger
        );

        await loader.LoadAsync(CreateItems(4).ToAsyncEnumerable());

        var info = logger.Entries.Where(e => e.Level == LogLevel.Information).Select(e => e.Message).ToList();
        Assert.Equal
        (
            "Loading started for ContractRecord. CommandText=" + InsertContractItem +
            ", Transaction=auto-managed, SkipItemCount=1, MaximumItemCount=2",
            info[0]
        );
        Assert.StartsWith("Loading completed for ContractRecord: 2 items loaded, 1 skipped in ", info[1], StringComparison.Ordinal);
        Assert.EndsWith("ms", info[1], StringComparison.Ordinal);
    }
}
