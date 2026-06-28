using Wolfgang.Etl.Abstractions;
using Wolfgang.Etl.TestKit.Xunit;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DbLoaderTests
    : LoaderBaseContractTests<
        DbLoader<ContractRecord>,
        ContractRecord,
        DbReport>
{
    // ------------------------------------------------------------------
    // Contract test factory methods
    // ------------------------------------------------------------------

    /// <inheritdoc/>
    protected override DbLoader<ContractRecord> CreateSut(int itemCount)
    {
        var conn = TestDb.CreateContractLoaderConnection();
        return new DbLoader<ContractRecord>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)"
        );
    }



    /// <inheritdoc/>
    protected override IReadOnlyList<ContractRecord> CreateSourceItems() => new[]
    {
        new ContractRecord { Name = "Item1", Value = 10 },
        new ContractRecord { Name = "Item2", Value = 20 },
        new ContractRecord { Name = "Item3", Value = 30 },
        new ContractRecord { Name = "Item4", Value = 40 },
        new ContractRecord { Name = "Item5", Value = 50 },
    };



    /// <inheritdoc/>
    protected override DbLoader<ContractRecord> CreateSutWithTimer(IProgressTimer timer)
    {
        var conn = TestDb.CreateContractLoaderConnection();
        return new DbLoader<ContractRecord>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            timer
        );
    }


    // ------------------------------------------------------------------
    // Constructor validation
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_when_connection_is_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => new DbLoader<PersonRecord>(null!, "INSERT INTO X VALUES (1)")
        );
    }



    [Fact]
    public void Constructor_when_commandText_is_null_throws_ArgumentNullException()
    {
        using var conn = TestDb.CreateConnection();
        Assert.Throws<ArgumentNullException>
        (
            () => new DbLoader<PersonRecord>(conn, (string)null!)
        );
    }



    // ------------------------------------------------------------------
    // Basic loading
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_inserts_all_records()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        await loader.LoadAsync
        (
            CreateTestRecords(3).ToAsyncEnumerable()
        );

        Assert.Equal(3, await TestDb.CountRowsAsync(conn));
        Assert.Equal(3, loader.CurrentItemCount);
    }



    [Fact]
    public async Task LoadAsync_with_empty_source_inserts_nothing()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        await loader.LoadAsync
        (
            Array.Empty<PersonRecord>().ToAsyncEnumerable()
        );

        Assert.Equal(0, await TestDb.CountRowsAsync(conn));
    }



    // ------------------------------------------------------------------
    // Auto-generated INSERT from attributes
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_with_auto_generated_insert_inserts_records()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            WriteMode.Insert
        );

        await loader.LoadAsync
        (
            CreateTestRecords(3).ToAsyncEnumerable()
        );

        Assert.Equal(3, await TestDb.CountRowsAsync(conn));
    }



    [Fact]
    public void CommandText_with_auto_generated_insert_contains_table_name()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, WriteMode.Insert);

        Assert.Contains("People", loader.CommandText, StringComparison.Ordinal);
        Assert.Contains("INSERT", loader.CommandText, StringComparison.OrdinalIgnoreCase);
    }



    // ------------------------------------------------------------------
    // SkipItemCount / MaximumItemCount
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_when_SkipItemCount_is_set_skips_records()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );
        loader.SkipItemCount = 2;

        await loader.LoadAsync
        (
            CreateTestRecords(5).ToAsyncEnumerable()
        );

        Assert.Equal(3, await TestDb.CountRowsAsync(conn));
    }



    [Fact]
    public async Task LoadAsync_when_MaximumItemCount_is_set_stops_early()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );
        loader.MaximumItemCount = 2;

        await loader.LoadAsync
        (
            CreateTestRecords(5).ToAsyncEnumerable()
        );

        Assert.Equal(2, await TestDb.CountRowsAsync(conn));
    }



    // ------------------------------------------------------------------
    // Transaction — auto-managed
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_without_transaction_auto_commits_on_success()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        await loader.LoadAsync
        (
            CreateTestRecords(3).ToAsyncEnumerable()
        );

        // Data persists after loader completes
        Assert.Equal(3, await TestDb.CountRowsAsync(conn));
    }



    [Fact]
    public async Task LoadAsync_without_transaction_auto_rolls_back_on_failure()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        // Create a source that fails partway through
        await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(FailingSourceAsync(2))
        );

        // Rolled back -- no rows persisted
        Assert.Equal(0, await TestDb.CountRowsAsync(conn));
    }



    // ------------------------------------------------------------------
    // Transaction — caller-managed
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_with_caller_transaction_does_not_commit()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);
#if NETFRAMEWORK
        using var transaction = conn.BeginTransaction();
#else
        using var transaction = await conn.BeginTransactionAsync();
#endif

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)",
            transaction
        );

        await loader.LoadAsync
        (
            CreateTestRecords(3).ToAsyncEnumerable()
        );

        // Loader did NOT commit -- caller must commit
#if NETFRAMEWORK
        transaction.Rollback();
#else
        await transaction.RollbackAsync();
#endif
        Assert.Equal(0, await TestDb.CountRowsAsync(conn));
    }



    [Fact]
    public async Task LoadAsync_with_caller_transaction_does_not_rollback_on_failure()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);
#if NETFRAMEWORK
        using var transaction = conn.BeginTransaction();
#else
        using var transaction = await conn.BeginTransactionAsync();
#endif

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)",
            transaction
        );

        // Loader throws but does NOT rollback the caller's transaction
        await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(FailingSourceAsync(2))
        );

        // Caller can still commit the partial work if desired
#if NETFRAMEWORK
        transaction.Commit();
#else
        await transaction.CommitAsync();
#endif
        Assert.Equal(2, await TestDb.CountRowsAsync(conn));
    }



    // ------------------------------------------------------------------
    // Progress report
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetProgressReport_returns_DbReport_with_counts()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        await loader.LoadAsync
        (
            CreateTestRecords(3).ToAsyncEnumerable()
        );

        var report = loader.GetProgressReport();

        Assert.Equal(3, report.CurrentItemCount);
        Assert.Contains("INSERT", report.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.True(report.ElapsedMilliseconds >= 0);
    }



    // ------------------------------------------------------------------
    // BatchSize
    // ------------------------------------------------------------------

    [Fact]
    public void BatchSize_default_is_one()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        Assert.Equal(1, loader.BatchSize);
    }



    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void BatchSize_when_less_than_one_throws_ArgumentOutOfRangeException(int badValue)
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        Assert.Throws<ArgumentOutOfRangeException>(() => loader.BatchSize = badValue);
    }



    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task LoadAsync_with_BatchSize_inserts_all_records(int batchSize)
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );
        loader.BatchSize = batchSize;

        await loader.LoadAsync(CreateTestRecords(7).ToAsyncEnumerable());

        Assert.Equal(7, await TestDb.CountRowsAsync(conn));
        Assert.Equal(7, loader.CurrentItemCount);
    }



    [Fact]
    public async Task LoadAsync_with_BatchSize_when_records_do_not_divide_evenly_flushes_remainder()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );
        loader.BatchSize = 4;

        // 10 records / batch 4 → 4 + 4 + 2 (remainder)
        await loader.LoadAsync(CreateTestRecords(10).ToAsyncEnumerable());

        Assert.Equal(10, await TestDb.CountRowsAsync(conn));
        Assert.Equal(10, loader.CurrentItemCount);
    }



    [Fact]
    public async Task LoadAsync_with_BatchSize_honors_SkipItemCount()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );
        loader.BatchSize = 3;
        loader.SkipItemCount = 4;

        await loader.LoadAsync(CreateTestRecords(10).ToAsyncEnumerable());

        // 10 - 4 skipped = 6 inserted
        Assert.Equal(6, await TestDb.CountRowsAsync(conn));
        Assert.Equal(6, loader.CurrentItemCount);
        Assert.Equal(4, loader.CurrentSkippedItemCount);
    }



    [Fact]
    public async Task LoadAsync_with_BatchSize_honors_MaximumItemCount()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );
        loader.BatchSize = 5;
        loader.MaximumItemCount = 7;

        await loader.LoadAsync(CreateTestRecords(20).ToAsyncEnumerable());

        Assert.Equal(7, await TestDb.CountRowsAsync(conn));
        Assert.Equal(7, loader.CurrentItemCount);
    }



    [Fact]
    public async Task LoadAsync_with_BatchSize_and_caller_transaction_does_not_commit()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);
#if NETFRAMEWORK
        using var transaction = conn.BeginTransaction();
#else
        using var transaction = await conn.BeginTransactionAsync();
#endif

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)",
            transaction
        );
        loader.BatchSize = 4;

        await loader.LoadAsync(CreateTestRecords(10).ToAsyncEnumerable());

#if NETFRAMEWORK
        transaction.Rollback();
#else
        await transaction.RollbackAsync();
#endif

        // After rollback, nothing persisted
        Assert.Equal(0, await TestDb.CountRowsAsync(conn));
    }



    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PersonRecord[] CreateTestRecords(int count)
    {
        var records = new PersonRecord[count];
        for (var i = 0; i < count; i++)
        {
            records[i] = new PersonRecord
            {
                FirstName = $"First{i + 1}",
                LastName = $"Last{i + 1}",
                Age = 20 + i + 1,
            };
        }
        return records;
    }



    private static async IAsyncEnumerable<PersonRecord> FailingSourceAsync(int succeedCount)
    {
        for (var i = 0; i < succeedCount; i++)
        {
            yield return new PersonRecord
            {
                FirstName = $"First{i + 1}",
                LastName = $"Last{i + 1}",
                Age = 20 + i + 1,
            };
        }

        await Task.CompletedTask;
        throw new InvalidOperationException("Simulated failure");
    }



    // ------------------------------------------------------------------
    // CommandTimeout (#25)
    // ------------------------------------------------------------------

    [Fact]
    public void CommandTimeout_defaults_to_null()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, "INSERT INTO People (first_name) VALUES (@FirstName)");

        Assert.Null(loader.CommandTimeout);
    }



    [Fact]
    public void CommandTimeout_set_and_get_roundtrips()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, "INSERT INTO People (first_name) VALUES (@FirstName)");

        loader.CommandTimeout = TimeSpan.FromMinutes(10);

        Assert.Equal(TimeSpan.FromMinutes(10), loader.CommandTimeout);
    }



    [Fact]
    public void CommandTimeout_when_set_to_negative_throws_ArgumentOutOfRangeException()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, "INSERT INTO People (first_name) VALUES (@FirstName)");

        Assert.Throws<ArgumentOutOfRangeException>
        (
            () => loader.CommandTimeout = TimeSpan.FromMilliseconds(-1)
        );
    }



    // ------------------------------------------------------------------
    // CommandType (#26)
    // ------------------------------------------------------------------

    [Fact]
    public void CommandType_defaults_to_Text()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, "INSERT INTO People (first_name) VALUES (@FirstName)");

        Assert.Equal(System.Data.CommandType.Text, loader.CommandType);
    }



    [Fact]
    public void CommandType_set_and_get_roundtrips()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, "usp_InsertPerson");

        loader.CommandType = System.Data.CommandType.StoredProcedure;

        Assert.Equal(System.Data.CommandType.StoredProcedure, loader.CommandType);
    }



    // ------------------------------------------------------------------
    // DbProviderFactory ctor (#28)
    // ------------------------------------------------------------------

    [Fact]
    public void DbProviderFactory_ctor_when_factory_is_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => new DbLoader<PersonRecord>((System.Data.Common.DbProviderFactory)null!, "Data Source=:memory:", "INSERT INTO People (first_name) VALUES (@FirstName)")
        );
    }



    [Fact]
    public void DbProviderFactory_ctor_when_connectionString_is_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => new DbLoader<PersonRecord>(Microsoft.Data.Sqlite.SqliteFactory.Instance, null!, "INSERT INTO People (first_name) VALUES (@FirstName)")
        );
    }



    [Fact]
    public void DbProviderFactory_ctor_when_commandText_is_null_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => new DbLoader<PersonRecord>(Microsoft.Data.Sqlite.SqliteFactory.Instance, "Data Source=:memory:", (string)null!)
        );
    }



    // ------------------------------------------------------------------
    // IsDryRun (#21)
    // ------------------------------------------------------------------

    [Fact]
    public void IsDryRun_defaults_to_false()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, "INSERT INTO People (first_name) VALUES (@FirstName)");

        Assert.False(loader.IsDryRun);
    }



    [Fact]
    public void IsDryRun_set_and_get_roundtrips()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord>(conn, "INSERT INTO People (first_name) VALUES (@FirstName)");

        loader.IsDryRun = true;

        Assert.True(loader.IsDryRun);
    }



    [Fact]
    public async Task LoadAsync_when_IsDryRun_is_true_does_not_write_to_database()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        )
        {
            IsDryRun = true
        };

        await loader.LoadAsync(CreateTestRecords(5).ToAsyncEnumerable());

        // Pipeline ran end-to-end (counter incremented for every record) but
        // the DB is untouched.
        Assert.Equal(0, await TestDb.CountRowsAsync(conn));
        Assert.Equal(5, loader.CurrentItemCount);
    }



    [Fact]
    public async Task LoadAsync_when_IsDryRun_is_true_and_BatchSize_is_set_does_not_flush_batches()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        )
        {
            IsDryRun = true,
            BatchSize = 3
        };

        await loader.LoadAsync(CreateTestRecords(7).ToAsyncEnumerable());

        Assert.Equal(0, await TestDb.CountRowsAsync(conn));
        Assert.Equal(7, loader.CurrentItemCount);
    }
}
