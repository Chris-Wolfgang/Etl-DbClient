using Microsoft.Extensions.Logging;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DbExtractorLoggingTests
{
    [Fact]
    public async Task ExtractAsync_logs_Information_at_start_and_completion()
    {
        var logger = new SpyLogger<DbExtractor<ContractRecord>>();
        using var conn = TestDb.CreateContractConnection(3);
        var extractor = new DbExtractor<ContractRecord>
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
        var logger = new SpyLogger<DbExtractor<ContractRecord>>();
        using var conn = TestDb.CreateContractConnection(3);
        var extractor = new DbExtractor<ContractRecord>
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
        var logger = new SpyLogger<DbExtractor<ContractRecord>>();
        using var conn = TestDb.CreateContractConnection(5);
        var extractor = new DbExtractor<ContractRecord>
        (
            conn,
            "SELECT Name, Value FROM ContractItems WHERE Value > @MinValue",
            new Dictionary<string, object>(StringComparer.Ordinal) { { "MinValue", 30 } },
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
        var logger = new SpyLogger<DbExtractor<ContractRecord>>();
        using var conn = TestDb.CreateContractConnection(5);
        var extractor = new DbExtractor<ContractRecord>
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
        var logger = new SpyLogger<DbExtractor<ContractRecord>>();
        using var conn = TestDb.CreateContractConnection(5);
        var extractor = new DbExtractor<ContractRecord>
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
        var extractor = new DbExtractor<ContractRecord>
        (
            conn,
            "SELECT Name, Value FROM ContractItems"
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
    }
}
