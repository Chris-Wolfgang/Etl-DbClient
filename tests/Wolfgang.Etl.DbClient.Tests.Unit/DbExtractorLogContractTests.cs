using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wolfgang.Etl.ErrorPolicies;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Fifth batch of mutation-survivor tests for #474: <see cref="DbExtractor{TRecord}"/>'s exact
/// start, completion and per-row log messages, the debug entry for a row skipped by
/// <see cref="ItemErrorPolicy.Skip"/>, and the guarantee that an extractor built on a caller's
/// connection leaves that connection open.
/// </summary>
/// <remarks>
/// A Stryker run over every source file except <c>DbLoader.cs</c> on top of batch 4 left these
/// survivors in <c>DbExtractor.cs</c>. The existing logging tests match fragments only, and no
/// test checked the caller's connection after an extraction, so flipping
/// <c>ownsConnection</c> to <c>true</c> (which disposes the caller's connection) survived.
/// </remarks>
public class DbExtractorLogContractTests
{
    private const string SelectContractItems = "SELECT Name, Value FROM ContractItems";



    [Fact]
    public async Task ExtractAsync_start_and_completion_messages_are_exact()
    {
        var logger = new SpyLogger<DbExtractor<ContractRecord>>();
        using var conn = TestDb.CreateContractConnection(5);
        var extractor = new DbExtractor<ContractRecord>
        (
            conn,
            SelectContractItems,
            new DbExtractorOptions { SkipItemCount = 1, MaximumItemCount = 2 },
            logger: logger
        );

        await extractor.ExtractAsync().ToListAsync();

        var info = logger.Entries.Where(e => e.Level == LogLevel.Information).Select(e => e.Message).ToList();
        Assert.Equal
        (
            "Extraction started for ContractRecord. CommandText=" + SelectContractItems +
            ", SkipItemCount=1, MaximumItemCount=2",
            info[0]
        );
        Assert.StartsWith("Extraction completed for ContractRecord: 2 items extracted, 1 skipped in ", info[1], StringComparison.Ordinal);
        Assert.EndsWith("ms", info[1], StringComparison.Ordinal);
    }



    [Fact]
    public async Task ExtractAsync_logs_Debug_with_the_row_index_and_the_item_number()
    {
        var logger = new SpyLogger<DbExtractor<ContractRecord>>();
        using var conn = TestDb.CreateContractConnection(2);
        var extractor = new DbExtractor<ContractRecord>(conn, SelectContractItems, options: null, logger: logger);

        await extractor.ExtractAsync().ToListAsync();

        var rows = logger.Entries
            .Where(e => e.Level == LogLevel.Debug && e.Message.StartsWith("Extracted row", StringComparison.Ordinal))
            .Select(e => e.Message)
            .ToList();
        Assert.Equal
        (
            new[] { "Extracted row 1 (item #1)", "Extracted row 2 (item #2)" },
            rows
        );
    }



    [Fact]
    public async Task ExtractAsync_when_ErrorPolicy_is_Skip_logs_Debug_naming_the_skipped_row()
    {
        var logger = new SpyLogger<DbExtractor<PersonRecord>>();
        using var conn = TestDb.CreateConnection();
        using (var seed = conn.CreateCommand())
        {
            // A non-numeric age fails Dapper's conversion to PersonRecord.Age on row 2.
            seed.CommandText = @"
                CREATE TABLE People (first_name TEXT, age INTEGER);
                INSERT INTO People (first_name, age) VALUES ('Ada', 30);
                INSERT INTO People (first_name, age) VALUES ('Bad', 'oops');
                INSERT INTO People (first_name, age) VALUES ('Zoe', 40);";
            await seed.ExecuteNonQueryAsync();
        }

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, age AS Age FROM People",
            new DbExtractorOptions { ErrorPolicy = ItemErrorPolicy.Skip },
            logger: logger
        );

        await extractor.ExtractAsync().ToListAsync();

        var skipped = Assert.Single
        (
            logger.Entries,
            e => e.Level == LogLevel.Debug && e.Message.Contains("skipped by ErrorPolicy", StringComparison.Ordinal)
        );
        Assert.Equal
        (
            "Row 2 skipped by ErrorPolicy: " + skipped.Exception!.Message,
            skipped.Message
        );
    }



    [Fact]
    public async Task ExtractAsync_with_parameters_leaves_the_caller_connection_open()
    {
        using var conn = TestDb.CreateContractConnection(3);
        var extractor = new DbExtractor<ContractRecord>
        (
            conn,
            SelectContractItems + " WHERE Value > @MinValue",
            new Dictionary<string, object>(StringComparer.Ordinal) { { "MinValue", 10 } },
            options: null
        );

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(ConnectionState.Open, conn.State);
    }



    [Fact]
    public async Task ExtractAsync_with_a_generated_select_leaves_the_caller_connection_open()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 3);
        var extractor = new DbExtractor<PersonRecord>(conn, options: null);

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(3, results.Count);
        Assert.Equal(ConnectionState.Open, conn.State);
    }
}
