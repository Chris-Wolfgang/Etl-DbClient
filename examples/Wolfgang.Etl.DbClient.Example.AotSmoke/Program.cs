// AOT / trim smoke consumer for Wolfgang.Etl.DbClient.
//
// This project is published with <PublishAot>true</PublishAot> +
// <PublishTrimmed>true</PublishTrimmed>. The point is to surface
// which library surfaces the trimmer and AOT compiler can't see
// through — Dapper's reflection-driven QueryAsync<T>, attribute
// discovery on TRecord types, dynamic generic instantiation.
//
// The `aot-smoke` workflow captures IL2xxx / AOT0xxx / IL3xxx
// warnings from `dotnet publish` and reports them. It is
// informational (non-blocking) today because Dapper 2.1.66 has
// known trim/AOT gaps that no consumer can annotate around from
// the outside. Blocking-mode is a follow-up, tracked in the
// aot-smoke workflow file.

using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;

namespace Wolfgang.Etl.DbClient.Example.AotSmoke;

[DbTable("widget")]
public record Widget
{
    [DbColumn("id")]
    public int Id { get; init; }

    [DbColumn("name")]
    public string Name { get; init; } = "";

    [DbColumn("price")]
    public decimal Price { get; init; }
}

internal static class Program
{
    private static async Task<int> Main()
    {
        Console.WriteLine("[aot-smoke] Wolfgang.Etl.DbClient AOT + trim smoke");
        Console.WriteLine("[aot-smoke] Runtime : " + Environment.Version);
        Console.WriteLine("[aot-smoke] Version : " + typeof(DbExtractor<Widget>).Assembly.GetName().Version);

        // In-memory SQLite so the smoke test needs no external service.
        // "Shared cache" so extractor + loader can talk to the same store.
        const string cs = "Data Source=:memory:";
        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        await using (var create = conn.CreateCommand())
        {
            create.CommandText = @"
                CREATE TABLE widget (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL NOT NULL);
                INSERT INTO widget (id, name, price) VALUES (1, 'sprocket', 1.99), (2, 'flange', 3.49), (3, 'grommet', 0.25);
            ";
            await create.ExecuteNonQueryAsync();
        }

        var extractor = new DbExtractor<Widget>(conn, "SELECT id, name, price FROM widget ORDER BY id");
        int rowCount = 0;
        await foreach (var w in extractor.ExtractAsync())
        {
            rowCount++;
            Console.WriteLine("[aot-smoke] extracted: " + w);
        }

        Console.WriteLine("[aot-smoke] extractor: CurrentItemCount=" + extractor.CurrentItemCount + " CurrentSkippedItemCount=" + extractor.CurrentSkippedItemCount);

        // Loader construction + dry-run: exercise the loader ctor + ISupportDryRun
        // path without needing to actually mutate the DB.
        var loader = new DbLoader<Widget>
        (
            conn,
            "INSERT INTO widget (id, name, price) VALUES (@Id, @Name, @Price)"
        )
        {
            IsDryRun = true
        };

        var toLoad = new[]
        {
            new Widget { Id = 4, Name = "washer", Price = 0.10m },
            new Widget { Id = 5, Name = "bolt", Price = 0.75m }
        };

        await loader.LoadAsync(ToAsyncEnumerable(toLoad));
        Console.WriteLine("[aot-smoke] loader: CurrentItemCount=" + loader.CurrentItemCount + " IsDryRun=" + loader.IsDryRun);

        // Attribute discovery — a common trim / AOT problem area for
        // libraries that route on custom attributes.
        var tableAttr = typeof(Widget).GetCustomAttributes(typeof(DbTableAttribute), inherit: false).OfType<DbTableAttribute>().FirstOrDefault();
        Console.WriteLine("[aot-smoke] DbTableAttribute.Name = " + (tableAttr?.Name ?? "<null>"));

        var columnCount = typeof(Widget).GetProperties()
            .SelectMany(p => p.GetCustomAttributes(typeof(DbColumnAttribute), inherit: false))
            .Count();
        Console.WriteLine("[aot-smoke] DbColumnAttribute count on Widget = " + columnCount);

        if (rowCount != 3)
        {
            Console.WriteLine("[aot-smoke] FAIL: expected 3 rows, got " + rowCount);
            return 1;
        }

        Console.WriteLine("[aot-smoke] OK");
        return 0;

        static async System.Collections.Generic.IAsyncEnumerable<T> ToAsyncEnumerable<T>(System.Collections.Generic.IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                yield return item;
                await Task.Yield();
            }
        }
    }
}
