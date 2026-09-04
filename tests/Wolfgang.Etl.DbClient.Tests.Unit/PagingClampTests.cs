using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// The page size sent to the database is clamped to what is still needed, so the server never
/// produces rows that would be discarded on arrival.
/// </summary>
/// <remarks>
/// These assert the <b>requested</b> limits, not the row counts. A wrong limit usually still
/// returns the right rows — asking for 100 and discarding 25 yields the same 75 as asking for
/// 75 — so a count-only test would pass while the optimisation did nothing.
/// </remarks>
public class PagingClampTests
{
    private static IReadOnlyList<(long Offset, long Limit)> RequestedPages(SpyLogger<DbExtractor<PersonRecord>> logger)
    {
        var pattern = new Regex(@"PageOffset=(\d+), PageLimit=(\d+)", RegexOptions.CultureInvariant);

        return logger.Entries
            .Select(entry => pattern.Match(entry.Message))
            .Where(match => match.Success)
            .Select(match => (long.Parse(match.Groups[1].Value), long.Parse(match.Groups[2].Value)))
            .ToList();
    }



    [Fact]
    public async Task A_page_larger_than_the_remaining_maximum_is_shortened_to_it()
    {
        // Chris's case: offset 25, page size 100, maximum 75. Asking for 100 would have the
        // database produce 25 rows that are thrown away here.
        var logger = new SpyLogger<DbExtractor<PersonRecord>>();
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 500);

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id",
            new DbExtractorOptions
            {
                PagingClauseTemplate = PagingClauseTemplates.Sqlite,
                ServerOffset = 25,
                ServerLimit = 100
            },
            logger: logger
        )
        {
            MaximumItemCount = 75
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(75, records.Count);
        Assert.Equal(new[] { (25L, 75L) }, RequestedPages(logger));
    }



    [Fact]
    public async Task The_final_page_shrinks_to_the_remaining_maximum()
    {
        // Maximum 150 with page size 100: 100, then 50 — not 100 twice.
        var logger = new SpyLogger<DbExtractor<PersonRecord>>();
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 500);

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id",
            new DbExtractorOptions
            {
                PagingClauseTemplate = PagingClauseTemplates.Sqlite,
                ServerLimit = 100
            },
            logger: logger
        )
        {
            MaximumItemCount = 150
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(150, records.Count);
        Assert.Equal(new[] { (0L, 100L), (100L, 50L) }, RequestedPages(logger));
    }



    [Fact]
    public async Task SkipItemCount_is_pushed_into_the_offset_instead_of_being_fetched()
    {
        // Reversed by #398. This used to request (0, 40) — fetch the 10 skipped rows and throw
        // them away here. The database can skip them instead, so the request is (10, 30) and
        // those rows never cross the wire. The SkipItemCount term in the clamp disappears with
        // them: nothing is fetched-then-discarded, so received == yielded.
        var logger = new SpyLogger<DbExtractor<PersonRecord>>();
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 500);

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id",
            new DbExtractorOptions
            {
                PagingClauseTemplate = PagingClauseTemplates.Sqlite,
                ServerLimit = 1000
            },
            logger: logger
        )
        {
            SkipItemCount = 10,
            MaximumItemCount = 30
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(30, records.Count);
        Assert.Equal("First11", records[0].FirstName);
        Assert.Equal(new[] { (10L, 30L) }, RequestedPages(logger));
    }



    [Fact]
    public async Task No_maximum_and_a_skip_does_not_overflow_the_clamp()
    {
        // MaximumItemCount defaults to int.MaxValue, so SkipItemCount + MaximumItemCount
        // overflows int the moment a skip is set. The clamp computes in long; if it did not,
        // stillNeeded would go negative and the extraction would return nothing.
        var logger = new SpyLogger<DbExtractor<PersonRecord>>();
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 20);

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id",
            new DbExtractorOptions
            {
                PagingClauseTemplate = PagingClauseTemplates.Sqlite,
                ServerLimit = 5
            },
            logger: logger
        )
        {
            SkipItemCount = 3
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(17, records.Count);
        Assert.Equal("First4", records[0].FirstName);
        Assert.All(RequestedPages(logger), page => Assert.Equal(5L, page.Limit));
    }


    [Fact]
    public async Task Server_side_skip_composes_with_ServerOffset()
    {
        // Start at 25, then skip 10 more → the database is asked for offset 35.
        var logger = new SpyLogger<DbExtractor<PersonRecord>>();
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 500);

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id",
            new DbExtractorOptions
            {
                PagingClauseTemplate = PagingClauseTemplates.Sqlite,
                ServerOffset = 25,
                ServerLimit = 10
            },
            logger: logger
        )
        {
            SkipItemCount = 10,
            MaximumItemCount = 5
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        // Rows are 1-based, so offset 35 is First36.
        Assert.Equal(5, records.Count);
        Assert.Equal("First36", records[0].FirstName);
        Assert.Equal(new[] { (35L, 5L) }, RequestedPages(logger));
    }



    [Fact]
    public async Task Server_side_skip_still_reports_the_skipped_count()
    {
        // The rows were skipped, just not locally. Reporting 0 here would read as the setting
        // having been ignored.
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 50);

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id",
            new DbExtractorOptions
            {
                PagingClauseTemplate = PagingClauseTemplates.Sqlite,
                ServerLimit = 10
            }
        )
        {
            SkipItemCount = 7
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(43, records.Count);
        Assert.Equal("First8", records[0].FirstName);
        Assert.Equal(7, extractor.CurrentSkippedItemCount);
    }



    [Fact]
    public async Task Skipping_without_paging_warns_that_it_is_the_slow_path()
    {
        // No ServerLimit means no offset to fold into, so the rows have to be fetched and
        // discarded. That still works — it is just worth telling the caller there is a faster way.
        var logger = new SpyLogger<DbExtractor<PersonRecord>>();
        using var conn = await TestDb.CreateConnectionWithDataAsync(rowCount: 20);

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People ORDER BY id",
            new DbExtractorOptions(),
            logger: logger
        )
        {
            SkipItemCount = 5
        };

        var records = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(15, records.Count);
        Assert.Equal("First6", records[0].FirstName);

        var warning = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Warning));
        Assert.Contains("ServerLimit", warning.Message, StringComparison.Ordinal);
        Assert.Contains("PagingClauseTemplate", warning.Message, StringComparison.Ordinal);
    }
}
