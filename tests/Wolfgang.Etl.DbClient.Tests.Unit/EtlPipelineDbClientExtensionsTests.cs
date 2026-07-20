// End-to-end tests for the EtlPipeline DbClient extensions (#280).
//
// Coverage:
//   1. Round-trip fixture: DbExtractor → DbLoader against the same
//      SQLite :memory: connection via the fluent chain.
//   2. Server-side paging (ServerLimit / ServerOffset / PagingClauseTemplate)
//      applied through the builder still routes to the underlying
//      DbExtractor and produces the expected page.
//   3. AsAsyncEnumerable escape hatch works without a sink.
//   4. Null-guard behaviour on every factory + terminator.
//   5. Existing-instance overloads share state with the caller's own
//      extractor / loader (setter calls on the builder mutate the
//      caller's instance).
//
// The "Cross-format test: read CSV via CsvExtractor, write rows via
// DbLoader" AC item is NOT covered here — ETL-Csv hasn't shipped the
// CsvExtractor pipeline extension yet (sibling issue). Will land in
// a follow-up once ETL-Csv catches up.

using System.Data;
using System.Data.Common;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.Abstractions;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class EtlPipelineDbClientExtensionsTests
{
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public sealed class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }



    private static SqliteConnection CreateSourceWithRows(int rowCount)
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE source (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);";
        cmd.ExecuteNonQuery();

        for (var i = 1; i <= rowCount; i++)
        {
            using var ins = conn.CreateCommand();
            ins.CommandText = $"INSERT INTO source (Id, Name) VALUES ({i}, 'w-{i}');";
            ins.ExecuteNonQuery();
        }
        return conn;
    }



    private static SqliteConnection CreateEmptyDestination()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE dest (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);";
        cmd.ExecuteNonQuery();
        return conn;
    }



    private static long CountRows(DbConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table};";
        return System.Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }



    [Fact]
    public async Task Fluent_chain_roundtrips_rows_from_extractor_to_loader()
    {
        using var src = CreateSourceWithRows(3);
        using var dest = CreateEmptyDestination();

        await EtlPipeline
            .Create()
            .DbExtractor<Widget>(src, "SELECT Id, Name FROM source ORDER BY Id")
            .DbLoader<Widget>(dest, "INSERT INTO dest (Id, Name) VALUES (@Id, @Name)")
            .RunAsync()
            .ConfigureAwait(false);

        Assert.Equal(3L, CountRows(dest, "dest"));
    }



    [Fact]
    public async Task Extractor_builder_setters_propagate_to_the_underlying_extractor()
    {
        // Server-side paging via the builder: ServerLimit=2, ServerOffset=1 →
        // pipeline yields exactly rows 2 and 3.
        using var src = CreateSourceWithRows(5);
        using var dest = CreateEmptyDestination();

        await EtlPipeline
            .Create()
            .DbExtractor<Widget>(src, "SELECT Id, Name FROM source ORDER BY Id")
            .ServerOffset(1)
            .ServerLimit(2)
            .DbLoader<Widget>(dest, "INSERT INTO dest (Id, Name) VALUES (@Id, @Name)")
            .RunAsync()
            .ConfigureAwait(false);

        Assert.Equal(2L, CountRows(dest, "dest"));

        using var check = dest.CreateCommand();
        check.CommandText = "SELECT Id FROM dest ORDER BY Id;";
        using var reader = check.ExecuteReader();
        Assert.True(reader.Read()); Assert.Equal(2L, reader.GetInt64(0));
        Assert.True(reader.Read()); Assert.Equal(3L, reader.GetInt64(0));
    }



    [Fact]
    public async Task AsAsyncEnumerable_escape_hatch_enumerates_without_a_sink()
    {
        using var src = CreateSourceWithRows(2);

        var collected = new List<Widget>();
        await foreach (var w in EtlPipeline
            .Create()
            .DbExtractor<Widget>(src, "SELECT Id, Name FROM source ORDER BY Id")
            .AsAsyncEnumerable()
            .ConfigureAwait(false))
        {
            collected.Add(w);
        }

        Assert.Equal(2, collected.Count);
        Assert.Equal("w-1", collected[0].Name);
        Assert.Equal("w-2", collected[1].Name);
    }



    [Fact]
    public async Task Loader_builder_ErrorHandling_flows_through_to_the_underlying_loader()
    {
        using var src = CreateSourceWithRows(3);
        using var dest = CreateEmptyDestination();

        // Insert row 2 into dest first so the loader's second row hits a PK
        // conflict. RowErrorHandling.Skip lets rows 1 and 3 land; row 2
        // silently fails.
        using (var seed = dest.CreateCommand())
        {
            seed.CommandText = "INSERT INTO dest (Id, Name) VALUES (2, 'pre-existing');";
            seed.ExecuteNonQuery();
        }

        var extractor = new DbExtractor<Widget>(src, "SELECT Id, Name FROM source ORDER BY Id");
        var loader = new DbLoader<Widget>(dest, "INSERT INTO dest (Id, Name) VALUES (@Id, @Name)")
        {
            InsertBatchSize = 1,
        };

        await EtlPipeline
            .Create()
            .DbExtractor(extractor)
            .DbLoader(loader)
            .ErrorHandling(RowErrorHandling.Skip)
            .RunAsync()
            .ConfigureAwait(false);

        // 3 rows total: rows 1 and 3 succeeded, row 2 was the pre-existing one.
        Assert.Equal(3L, CountRows(dest, "dest"));
    }



    [Fact]
    public async Task Existing_extractor_overload_shares_state_with_the_caller()
    {
        using var src = CreateSourceWithRows(3);
        using var dest = CreateEmptyDestination();

        var extractor = new DbExtractor<Widget>(src, "SELECT Id, Name FROM source ORDER BY Id");
        Assert.Null(extractor.ServerLimit);

        // DbExtractor's paging is gated on BOTH ServerOffset AND ServerLimit
        // being set (see DbExtractor.ApplyServerPaging), so setting both
        // proves the setter path AND exercises paging end-to-end.
        await EtlPipeline
            .Create()
            .DbExtractor(extractor)
            .ServerOffset(0)
            .ServerLimit(2)
            .DbLoader<Widget>(dest, "INSERT INTO dest (Id, Name) VALUES (@Id, @Name)")
            .RunAsync()
            .ConfigureAwait(false);

        // Setter on the builder mutated the caller's extractor.
        Assert.Equal(0L, extractor.ServerOffset);
        Assert.Equal(2L, extractor.ServerLimit);
        Assert.Equal(2L, CountRows(dest, "dest"));
    }



    [Fact]
    public void DbExtractor_factories_throw_ArgumentNullException_on_null_inputs()
    {
        using var src = CreateSourceWithRows(1);
        var pipeline = EtlPipeline.Create();

        Assert.Throws<System.ArgumentNullException>(() =>
            ((EtlPipeline)null!).DbExtractor<Widget>(src, "SELECT 1"));
        Assert.Throws<System.ArgumentNullException>(() =>
            pipeline.DbExtractor<Widget>((DbConnection)null!, "SELECT 1"));
        Assert.Throws<System.ArgumentNullException>(() =>
            pipeline.DbExtractor<Widget>(src, (string)null!));
        Assert.Throws<System.ArgumentNullException>(() =>
            pipeline.DbExtractor<Widget>((DbExtractor<Widget>)null!));
    }



    [Fact]
    public void DbLoader_factories_throw_ArgumentNullException_on_null_inputs()
    {
        using var src = CreateSourceWithRows(1);
        using var dest = CreateEmptyDestination();
        var stage = EtlPipeline
            .Create()
            .DbExtractor<Widget>(src, "SELECT Id, Name FROM source");

        Assert.Throws<System.ArgumentNullException>(() =>
            ((IEtlPipeline<Widget>)null!).DbLoader<Widget>(dest, "INSERT ..."));
        Assert.Throws<System.ArgumentNullException>(() =>
            stage.DbLoader<Widget>((DbConnection)null!, "INSERT ..."));
        Assert.Throws<System.ArgumentNullException>(() =>
            stage.DbLoader<Widget>(dest, (string)null!));
        Assert.Throws<System.ArgumentNullException>(() =>
            stage.DbLoader<Widget>((DbLoader<Widget>)null!));
    }
}
