using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DbLoaderLoggingTests
{
    private static ContractRecord[] CreateItems(int count)
    {
        var items = new ContractRecord[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = new ContractRecord { Name = $"Item{i + 1}", Value = (i + 1) * 10 };
        }
        return items;
    }



    [Fact]
    public async Task LoadAsync_logs_Information_at_start_and_completion()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord, DbReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord, DbReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            logger: logger
        );

        await loader.LoadAsync(CreateItems(3).ToAsyncEnumerable());

        var info = logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.Equal(2, info.Count);
        Assert.Contains("Loading started", info[0].Message, StringComparison.Ordinal);
        Assert.Contains("Loading completed", info[1].Message, StringComparison.Ordinal);
        Assert.Contains("3 items loaded", info[1].Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task LoadAsync_logs_Debug_per_record()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord, DbReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord, DbReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            logger: logger
        );

        await loader.LoadAsync(CreateItems(3).ToAsyncEnumerable());

        var debugRecords = logger.Entries
            .Where(e => e.Level == LogLevel.Debug && e.Message.Contains("Loaded record", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, debugRecords.Count);
    }



    [Fact]
    public async Task LoadAsync_logs_Debug_transaction_lifecycle()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord, DbReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord, DbReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            logger: logger
        );

        await loader.LoadAsync(CreateItems(2).ToAsyncEnumerable());

        Assert.Contains
        (
            logger.Entries,
            e => e.Level == LogLevel.Debug
                && e.Message.Contains("transaction created", StringComparison.Ordinal)
        );
        Assert.Contains
        (
            logger.Entries,
            e => e.Level == LogLevel.Debug
                && e.Message.Contains("transaction committed", StringComparison.Ordinal)
        );
    }



    [Fact]
    public async Task LoadAsync_on_failure_logs_Debug_rollback()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord, DbReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord, DbReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            logger: logger
        );

        await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(FailingSourceAsync()).ConfigureAwait(false)
        );

        Assert.Contains
        (
            logger.Entries,
            e => e.Level == LogLevel.Debug
                && e.Message.Contains("rolled back", StringComparison.Ordinal)
        );
    }



    [Fact]
    public async Task LoadAsync_logs_auto_managed_transaction_mode()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord, DbReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord, DbReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            logger: logger
        );

        await loader.LoadAsync(CreateItems(1).ToAsyncEnumerable());

        var startMsg = logger.Entries.First(e => e.Level == LogLevel.Information);
        Assert.Contains("auto-managed", startMsg.Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task LoadAsync_with_caller_transaction_logs_caller_managed()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord, DbReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        using var transaction = conn.BeginTransaction();
        var loader = new DbLoader<ContractRecord, DbReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            transaction,
            logger
        );

        await loader.LoadAsync(CreateItems(1).ToAsyncEnumerable());
        transaction.Commit();

        var startMsg = logger.Entries.First(e => e.Level == LogLevel.Information);
        Assert.Contains("caller-managed", startMsg.Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task LoadAsync_when_no_logger_does_not_throw()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord, DbReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)"
        );

        await loader.LoadAsync(CreateItems(3).ToAsyncEnumerable());

        Assert.Equal(3, loader.CurrentItemCount);
    }



    [Fact]
    public async Task LoadAsync_when_SkipItemCount_set_logs_Debug_skipped()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord, DbReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord, DbReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            logger: logger
        );
        loader.SkipItemCount = 2;

        await loader.LoadAsync(CreateItems(5).ToAsyncEnumerable());

        Assert.Contains
        (
            logger.Entries,
            e => e.Level == LogLevel.Debug
                && e.Message.Contains("Skipping item", StringComparison.Ordinal)
        );
    }



    [Fact]
    public async Task LoadAsync_when_MaximumItemCount_reached_logs_Debug()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord, DbReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord, DbReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            logger: logger
        );
        loader.MaximumItemCount = 2;

        await loader.LoadAsync(CreateItems(5).ToAsyncEnumerable());

        Assert.Contains
        (
            logger.Entries,
            e => e.Level == LogLevel.Debug
                && e.Message.Contains("MaximumItemCount", StringComparison.Ordinal)
        );
    }



    private static async IAsyncEnumerable<ContractRecord> FailingSourceAsync()
    {
        yield return new ContractRecord { Name = "Item1", Value = 10 };
        await Task.CompletedTask;
        throw new InvalidOperationException("Simulated failure");
    }
}
