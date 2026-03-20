using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wolfgang.Etl.Abstractions;
using Xunit;

namespace Wolfgang.Etl.Ado.Tests.Unit;

// ------------------------------------------------------------------
// Spy logger
// ------------------------------------------------------------------

[ExcludeFromCodeCoverage]
internal sealed class SpyLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = new();



    public IReadOnlyList<LogEntry> Entries => _entries;



    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;



    public bool IsEnabled(LogLevel logLevel) => true;



    public void Log<TState>
    (
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        _entries.Add
        (
            new LogEntry
            (
                logLevel,
                formatter(state, exception),
                exception
            )
        );
    }
}



[ExcludeFromCodeCoverage]
internal sealed class LogEntry
{
    public LogEntry(LogLevel level, string message, Exception? exception)
    {
        Level = level;
        Message = message;
        Exception = exception;
    }



    public LogLevel Level { get; }
    public string Message { get; }
    public Exception? Exception { get; }
}



// ------------------------------------------------------------------
// Extractor logging tests
// ------------------------------------------------------------------

public class AdoExtractorLoggingTests
{
    [Fact]
    public async Task ExtractAsync_logs_Information_at_start_and_completion()
    {
        var logger = new SpyLogger<AdoExtractor<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractConnection(3);
        var extractor = new AdoExtractor<ContractRecord, AdoReport>
        (
            conn,
            "SELECT Name, Value FROM ContractItems",
            logger: logger
        );

        await extractor.ExtractAsync().ToListAsync();

        var info = logger.Entries.Where(e => e.Level == LogLevel.Information).ToList();
        Assert.Equal(2, info.Count);
        Assert.Contains("Extraction started", info[0].Message, StringComparison.Ordinal);
        Assert.Contains("Extraction completed", info[1].Message, StringComparison.Ordinal);
        Assert.Contains("3 items extracted", info[1].Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task ExtractAsync_logs_Debug_per_row()
    {
        var logger = new SpyLogger<AdoExtractor<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractConnection(3);
        var extractor = new AdoExtractor<ContractRecord, AdoReport>
        (
            conn,
            "SELECT Name, Value FROM ContractItems",
            logger: logger
        );

        await extractor.ExtractAsync().ToListAsync();

        var debugRows = logger.Entries
            .Where(e => e.Level == LogLevel.Debug && e.Message.Contains("Extracted row", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, debugRows.Count);
    }



    [Fact]
    public async Task ExtractAsync_logs_Debug_parameters()
    {
        var logger = new SpyLogger<AdoExtractor<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractConnection(5);
        var extractor = new AdoExtractor<ContractRecord, AdoReport>
        (
            conn,
            "SELECT Name, Value FROM ContractItems WHERE Value > @MinValue",
            new Dictionary<string, object> { { "MinValue", 30 } },
            logger: logger
        );

        await extractor.ExtractAsync().ToListAsync();

        Assert.Contains
        (
            logger.Entries,
            e => e.Level == LogLevel.Debug
                && e.Message.Contains("MinValue", StringComparison.Ordinal)
        );
    }



    [Fact]
    public async Task ExtractAsync_when_SkipItemCount_set_logs_Debug_skipped()
    {
        var logger = new SpyLogger<AdoExtractor<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractConnection(5);
        var extractor = new AdoExtractor<ContractRecord, AdoReport>
        (
            conn,
            "SELECT Name, Value FROM ContractItems",
            logger: logger
        );
        extractor.SkipItemCount = 2;

        await extractor.ExtractAsync().ToListAsync();

        Assert.Contains
        (
            logger.Entries,
            e => e.Level == LogLevel.Debug
                && e.Message.Contains("Skipping row", StringComparison.Ordinal)
        );
    }



    [Fact]
    public async Task ExtractAsync_when_MaximumItemCount_reached_logs_Debug()
    {
        var logger = new SpyLogger<AdoExtractor<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractConnection(5);
        var extractor = new AdoExtractor<ContractRecord, AdoReport>
        (
            conn,
            "SELECT Name, Value FROM ContractItems",
            logger: logger
        );
        extractor.MaximumItemCount = 2;

        await extractor.ExtractAsync().ToListAsync();

        Assert.Contains
        (
            logger.Entries,
            e => e.Level == LogLevel.Debug
                && e.Message.Contains("MaximumItemCount", StringComparison.Ordinal)
        );
    }



    [Fact]
    public async Task ExtractAsync_when_no_logger_does_not_throw()
    {
        using var conn = TestDb.CreateContractConnection(3);
        var extractor = new AdoExtractor<ContractRecord, AdoReport>
        (
            conn,
            "SELECT Name, Value FROM ContractItems"
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
    }
}



// ------------------------------------------------------------------
// Loader logging tests
// ------------------------------------------------------------------

public class AdoLoaderLoggingTests
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
        var logger = new SpyLogger<AdoLoader<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new AdoLoader<ContractRecord, AdoReport>
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
        var logger = new SpyLogger<AdoLoader<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new AdoLoader<ContractRecord, AdoReport>
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
        var logger = new SpyLogger<AdoLoader<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new AdoLoader<ContractRecord, AdoReport>
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
        var logger = new SpyLogger<AdoLoader<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new AdoLoader<ContractRecord, AdoReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)",
            logger: logger
        );

        await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(FailingSource())
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
        var logger = new SpyLogger<AdoLoader<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        var loader = new AdoLoader<ContractRecord, AdoReport>
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
        var logger = new SpyLogger<AdoLoader<ContractRecord, AdoReport>>();
        using var conn = TestDb.CreateContractLoaderConnection();
        using var transaction = conn.BeginTransaction();
        var loader = new AdoLoader<ContractRecord, AdoReport>
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
        var loader = new AdoLoader<ContractRecord, AdoReport>
        (
            conn,
            "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)"
        );

        await loader.LoadAsync(CreateItems(3).ToAsyncEnumerable());

        Assert.Equal(3, loader.CurrentItemCount);
    }



    private static async IAsyncEnumerable<ContractRecord> FailingSource()
    {
        yield return new ContractRecord { Name = "Item1", Value = 10 };
        await Task.CompletedTask;
        throw new InvalidOperationException("Simulated failure");
    }
}
