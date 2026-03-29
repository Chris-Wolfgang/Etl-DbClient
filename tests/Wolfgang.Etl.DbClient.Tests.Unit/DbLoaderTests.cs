using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wolfgang.Etl.Abstractions;
using Wolfgang.Etl.TestKit.Xunit;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DbLoaderTests
    : LoaderBaseContractTests<
        DbLoader<ContractRecord, DbReport>,
        ContractRecord,
        DbReport>
{
    // ------------------------------------------------------------------
    // Contract test factory methods
    // ------------------------------------------------------------------

    /// <inheritdoc/>
    protected override DbLoader<ContractRecord, DbReport> CreateSut(int itemCount)
    {
        var conn = TestDb.CreateContractLoaderConnection();
        return new DbLoader<ContractRecord, DbReport>
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
    protected override DbLoader<ContractRecord, DbReport> CreateSutWithTimer(IProgressTimer timer)
    {
        var conn = TestDb.CreateContractLoaderConnection();
        return new DbLoader<ContractRecord, DbReport>
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
            () => new DbLoader<PersonRecord, DbReport>(null!, "INSERT INTO X VALUES (1)")
        );
    }



    [Fact]
    public void Constructor_when_commandText_is_null_throws_ArgumentNullException()
    {
        using var conn = TestDb.CreateConnection();
        Assert.Throws<ArgumentNullException>
        (
            () => new DbLoader<PersonRecord, DbReport>(conn, (string)null!)
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

        var loader = new DbLoader<PersonRecord, DbReport>
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

        var loader = new DbLoader<PersonRecord, DbReport>
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

        var loader = new DbLoader<PersonRecord, DbReport>
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
        var loader = new DbLoader<PersonRecord, DbReport>(conn, WriteMode.Insert);

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

        var loader = new DbLoader<PersonRecord, DbReport>
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

        var loader = new DbLoader<PersonRecord, DbReport>
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

        var loader = new DbLoader<PersonRecord, DbReport>
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

        var loader = new DbLoader<PersonRecord, DbReport>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        // Create a source that fails partway through
        await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(FailingSource(2))
        );

        // Rolled back — no rows persisted
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
        using var transaction = conn.BeginTransaction();

        var loader = new DbLoader<PersonRecord, DbReport>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)",
            transaction
        );

        await loader.LoadAsync
        (
            CreateTestRecords(3).ToAsyncEnumerable()
        );

        // Loader did NOT commit — caller must commit
        transaction.Rollback();
        Assert.Equal(0, await TestDb.CountRowsAsync(conn));
    }



    [Fact]
    public async Task LoadAsync_with_caller_transaction_does_not_rollback_on_failure()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);
        using var transaction = conn.BeginTransaction();

        var loader = new DbLoader<PersonRecord, DbReport>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)",
            transaction
        );

        // Loader throws but does NOT rollback the caller's transaction
        await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(FailingSource(2))
        );

        // Caller can still commit the partial work if desired
        transaction.Commit();
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

        var loader = new DbLoader<PersonRecord, DbReport>
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
    // Progress report — NotSupportedException
    // ------------------------------------------------------------------

    [Fact]
    public void GetProgressReport_when_TProgress_is_not_DbReport_throws_NotSupportedException()
    {
        using var conn = TestDb.CreateConnection();
        var loader = new DbLoader<PersonRecord, Exception>(conn, "INSERT INTO X VALUES (1)");

        Assert.Throws<NotSupportedException>(loader.GetProgressReport);
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



    private static async IAsyncEnumerable<PersonRecord> FailingSource(int succeedCount)
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
}
