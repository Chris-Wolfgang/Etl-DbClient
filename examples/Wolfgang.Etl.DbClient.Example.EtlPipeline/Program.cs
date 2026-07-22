using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.Abstractions;
using Wolfgang.Etl.DbClient;
using Wolfgang.Etl.DbClient.Example.EtlPipeline;

// ---------------------------------------------------------------
// Example: fluent EtlPipeline chain over DbExtractor / DbLoader (#280)
//
// Demonstrates the v0.7.0+ EtlPipeline extension surface:
//   * DbExtractor<T>(this EtlPipeline, DbConnection, string, DbTransaction?)
//   * DbLoader<T>(this IEtlPipeline<T>, DbConnection, string, DbTransaction?)
// plus the fluent builder setters that mirror the underlying extractor / loader.
//
// Everything runs against two in-memory SQLite databases so the example is
// self-contained and byte-repeatable.
// ---------------------------------------------------------------

// Set up an in-memory source with 20 orders and an empty destination.
using var src = new SqliteConnection("Data Source=:memory:");
using var dest = new SqliteConnection("Data Source=:memory:");
await src.OpenAsync().ConfigureAwait(false);
await dest.OpenAsync().ConfigureAwait(false);

using (var cmd = src.CreateCommand())
{
    cmd.CommandText = @"
        CREATE TABLE Orders (
            Id INTEGER PRIMARY KEY,
            Customer TEXT NOT NULL,
            Total REAL NOT NULL,
            Status TEXT NOT NULL
        );
    ";
    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
}

for (var i = 1; i <= 20; i++)
{
    using var ins = src.CreateCommand();
    ins.CommandText =
        $"INSERT INTO Orders (Id, Customer, Total, Status) VALUES ({i}, 'c{i}', {i * 10.5}, '{(i % 3 == 0 ? "pending" : "paid")}');";
    await ins.ExecuteNonQueryAsync().ConfigureAwait(false);
}

using (var cmd = dest.CreateCommand())
{
    cmd.CommandText = "CREATE TABLE PaidOrders (Id INTEGER PRIMARY KEY, Customer TEXT NOT NULL, Total REAL NOT NULL);";
    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
}


// -----------------------------------------------------------------
// 1. End-to-end pipeline: SQL query on `src` → INSERT on `dest`
// -----------------------------------------------------------------
Console.WriteLine("== 1. round-trip via fluent chain ==");

await EtlPipeline
    .Create()
    .DbExtractor<Order>(src, "SELECT Id, Customer, Total FROM Orders WHERE Status = 'paid' ORDER BY Id")
    .DbLoader<Order>(dest, "INSERT INTO PaidOrders (Id, Customer, Total) VALUES (@Id, @Customer, @Total)")
    .RunAsync()
    .ConfigureAwait(false);

Console.WriteLine($"   paid orders copied: {await dest.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM PaidOrders;").ConfigureAwait(false)}");


// -----------------------------------------------------------------
// 2. Server-side paging via the builder
// -----------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("== 2. server-side paging (skip 5, take 5) ==");

using var page = new SqliteConnection("Data Source=:memory:");
await page.OpenAsync().ConfigureAwait(false);
using (var cmd = page.CreateCommand())
{
    cmd.CommandText = "CREATE TABLE OrdersPage (Id INTEGER PRIMARY KEY, Customer TEXT NOT NULL, Total REAL NOT NULL);";
    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
}

await EtlPipeline
    .Create()
    .DbExtractor<Order>(src, "SELECT Id, Customer, Total FROM Orders ORDER BY Id")
    .ServerOffset(5)
    .ServerLimit(5)
    .DbLoader<Order>(page, "INSERT INTO OrdersPage (Id, Customer, Total) VALUES (@Id, @Customer, @Total)")
    .RunAsync()
    .ConfigureAwait(false);

Console.WriteLine($"   rows on page: {await page.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM OrdersPage;").ConfigureAwait(false)}");


// -----------------------------------------------------------------
// 3. Escape hatch: AsAsyncEnumerable + LINQ aggregation, no sink
// -----------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("== 3. escape hatch: sum totals inline ==");

double sum = 0;
await foreach (var order in EtlPipeline
    .Create()
    .DbExtractor<Order>(src, "SELECT Id, Customer, Total FROM Orders WHERE Status = 'paid'")
    .AsAsyncEnumerable()
    .ConfigureAwait(false))
{
    sum += order.Total;
}

Console.WriteLine($"   sum of paid totals: {sum:F2}");


// -----------------------------------------------------------------
// 4. Error handling: RowErrorHandling.Skip on the loader builder
// -----------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("== 4. RowErrorHandling.Skip continues past PK conflict ==");

// Pre-insert row Id=3 so the pipeline hits a PK conflict on it.
using (var seed = dest.CreateCommand())
{
    seed.CommandText = "DELETE FROM PaidOrders; INSERT INTO PaidOrders (Id, Customer, Total) VALUES (3, 'preexisting', 0.0);";
    await seed.ExecuteNonQueryAsync().ConfigureAwait(false);
}

var loader = new DbLoader<Order>
(
    dest,
    "INSERT INTO PaidOrders (Id, Customer, Total) VALUES (@Id, @Customer, @Total)"
)
{
    InsertBatchSize = 1,
};

await EtlPipeline
    .Create()
    .DbExtractor<Order>(src, "SELECT Id, Customer, Total FROM Orders WHERE Status = 'paid' ORDER BY Id")
    .DbLoader(loader)
    .ErrorHandling(RowErrorHandling.Skip)
    .RunAsync()
    .ConfigureAwait(false);

Console.WriteLine($"   total rows in dest: {await dest.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM PaidOrders;").ConfigureAwait(false)} (row 3 was pre-existing; the rest landed)");


// -----------------------------------------------------------------
// 5. Progress reporting
// -----------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("== 5. progress reporting via IProgress<EtlPipelineProgress> ==");

using var live = new SqliteConnection("Data Source=:memory:");
await live.OpenAsync().ConfigureAwait(false);
using (var cmd = live.CreateCommand())
{
    cmd.CommandText = "CREATE TABLE Live (Id INTEGER PRIMARY KEY, Customer TEXT NOT NULL, Total REAL NOT NULL);";
    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
}

var progress = new Progress<EtlPipelineProgress>(p =>
    Console.WriteLine($"   ↪ loaded={p.RecordsLoaded}"));

await EtlPipeline
    .Create()
    .DbExtractor<Order>(src, "SELECT Id, Customer, Total FROM Orders ORDER BY Id")
    .DbLoader<Order>(live, "INSERT INTO Live (Id, Customer, Total) VALUES (@Id, @Customer, @Total)")
    .RunAsync(progress)
    .ConfigureAwait(false);


Console.WriteLine();
Console.WriteLine("Done.");


// -----------------------------------------------------------------
// Types
// -----------------------------------------------------------------

namespace Wolfgang.Etl.DbClient.Example.EtlPipeline
{
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    internal sealed class Order
    {
        public int Id { get; set; }
        public string Customer { get; set; } = "";
        public double Total { get; set; }
    }
}
