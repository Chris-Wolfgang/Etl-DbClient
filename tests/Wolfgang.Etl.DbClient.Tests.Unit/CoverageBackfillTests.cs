// Targeted unit tests written for the v0.5.0 release-prep coverage backfill.
// The classes/branches exercised here were exposed as <95% coverage by the
// pre-release scan; this file brings them above the per-assembly gate without
// changing production behavior.

using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class CoverageBackfillTests
{
    // ------------------------------------------------------------------
    // DbColumnAttribute (was 0% — new in O09 source-generator PR)
    // ------------------------------------------------------------------

    [Fact]
    public void DbColumnAttribute_constructor_assigns_name()
    {
        var attr = new DbColumnAttribute("first_name");

        Assert.Equal("first_name", attr.Name);
        Assert.False(attr.Skip);
    }



    [Fact]
    public void DbColumnAttribute_constructor_when_name_is_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DbColumnAttribute(null!));
    }



    [Fact]
    public void DbColumnAttribute_Skip_set_and_get_roundtrips()
    {
        var attr = new DbColumnAttribute("col") { Skip = true };

        Assert.True(attr.Skip);
    }



    // ------------------------------------------------------------------
    // DbTableAttribute (was 0% — new in O09)
    // ------------------------------------------------------------------

    [Fact]
    public void DbTableAttribute_constructor_assigns_name()
    {
        var attr = new DbTableAttribute("orders");

        Assert.Equal("orders", attr.Name);
    }



    [Fact]
    public void DbTableAttribute_constructor_when_name_is_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DbTableAttribute(null!));
    }



    // ------------------------------------------------------------------
    // RowFailedEventArgs (was 75%)
    // ------------------------------------------------------------------

    [Fact]
    public void RowFailedEventArgs_constructor_when_exception_is_null_throws()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => new RowFailedEventArgs<string>("record", null!, itemIndex: 1)
        );
    }



    [Fact]
    public void RowFailedEventArgs_exposes_constructor_arguments()
    {
        var ex = new InvalidOperationException("boom");
        var args = new RowFailedEventArgs<string>("rec", ex, itemIndex: 42);

        Assert.Equal("rec", args.Record);
        Assert.Same(ex, args.Exception);
        Assert.Equal(42, args.ItemIndex);
    }



    // ------------------------------------------------------------------
    // DbExtractor.CountAsync owned-connection path (was 50%)
    // ------------------------------------------------------------------

    [Fact]
    public async Task CountAsync_with_owned_connection_opens_runs_and_disposes()
    {
        // Use a shared-cache in-memory DB so the extractor's owned connection
        // sees the keeper's seeded data.
        var connString = $"Data Source=count_owned_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var keeper = new SqliteConnection(connString);
        await keeper.OpenAsync();
        await TestDb.CreateEmptyTableAsync(keeper);
        using (var seed = keeper.CreateCommand())
        {
            seed.CommandText = "INSERT INTO People (first_name, last_name, age) VALUES ('A','B',1),('C','D',2),('E','F',3)";
            await seed.ExecuteNonQueryAsync();
        }

        var extractor = new DbExtractor<PersonRecord>
        (
            SqliteFactory.Instance,
            connString,
            "SELECT first_name AS FirstName, last_name AS LastName, age AS Age FROM People"
        );

        var count = await extractor.CountAsync();

        Assert.Equal(3, count);
    }



    // ------------------------------------------------------------------
    // DbLoader DbProviderFactory ctor success path (was uncovered)
    // ------------------------------------------------------------------

    [Fact]
    public async Task DbLoader_DbProviderFactory_ctor_opens_loads_and_disposes()
    {
        var connString = $"Data Source=loader_owned_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using var keeper = new SqliteConnection(connString);
        await keeper.OpenAsync();
        await TestDb.CreateEmptyTableAsync(keeper);

        var loader = new DbLoader<PersonRecord>
        (
            SqliteFactory.Instance,
            connString,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        );

        await loader.LoadAsync(CreateRecords(3).ToAsyncEnumerable());

        Assert.Equal(3, await TestDb.CountRowsAsync(keeper));
    }



    // ------------------------------------------------------------------
    // DbLoader.InsertBatchSize > 1 error paths (was uncovered)
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_VALUES_clause_is_not_parenthesized_throws()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name) VALUES 'not-parenthesized'"
        )
        {
            InsertBatchSize = 3
        };

        await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(CreateRecords(3).ToAsyncEnumerable())
        );
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_template_parameter_has_no_matching_property_throws()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name) VALUES (@NoSuchProperty)"
        )
        {
            InsertBatchSize = 3
        };

        await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(CreateRecords(3).ToAsyncEnumerable())
        );
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_and_IsDryRun_skips_SQL_and_increments_counters()
    {
        using var conn = TestDb.CreateConnection();
        await TestDb.CreateEmptyTableAsync(conn);

        var loader = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)"
        )
        {
            InsertBatchSize = 3,
            IsDryRun = true
        };

        await loader.LoadAsync(CreateRecords(5).ToAsyncEnumerable());

        Assert.Equal(0, await TestDb.CountRowsAsync(conn));   // DryRun: no writes
        Assert.Equal(5, loader.CurrentItemCount);             // counters still advance
    }



    // ------------------------------------------------------------------
    // DbCommandBuilder.BuildUpdate with all-NotMapped columns (was uncovered)
    // ------------------------------------------------------------------

    [Fact]
    public void BuildUpdate_when_all_properties_are_NotMapped_throws_InvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>
        (
            DbCommandBuilder.BuildUpdate<AllNotMappedUpdateRecord>
        );

        Assert.Contains("no mapped columns", ex.Message, StringComparison.Ordinal);
    }



    [ExcludeFromCodeCoverage]
    [System.ComponentModel.DataAnnotations.Schema.Table("AllNotMappedForUpdate")]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    private class AllNotMappedUpdateRecord
    {
        [System.ComponentModel.DataAnnotations.Key]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int Id { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string Name { get; set; } = string.Empty;
    }



    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PersonRecord[] CreateRecords(int count)
    {
        var arr = new PersonRecord[count];
        for (var i = 0; i < count; i++)
        {
            arr[i] = new PersonRecord
            {
                FirstName = $"First{i + 1}",
                LastName = $"Last{i + 1}",
                Age = 20 + i + 1,
            };
        }
        return arr;
    }
}
