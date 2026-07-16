// Sustained-load workload for GC / allocation profiling.
//
// Runs an extract + load loop against in-memory SQLite for a wall-clock
// duration configured via env / CLI. Designed to be run under
// `dotnet-counters collect` or `dotnet-trace` from a scheduled workflow
// so we can characterise gen0/1/2 promotion rates, LOH pressure,
// finalizer queue depth, and thread-pool starvation under a real ETL
// pattern.
//
// Not intended as a benchmark — the scale / iteration counts are
// arbitrary. Meaningful metrics come from the ETW / EventPipe trace
// captured by the outer workflow, not from the wall time this process
// reports.
//
// Refs #142.

using System.Data.Common;
using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;

const int rowsPerBatch = 5_000;
var durationSeconds = ParseDuration(args);

Console.WriteLine($"[gc-workload] Version : {typeof(DbExtractor<>).Assembly.GetName().Version}");
Console.WriteLine($"[gc-workload] Runtime : {Environment.Version}");
Console.WriteLine($"[gc-workload] ServerGC : {System.Runtime.GCSettings.IsServerGC}");
Console.WriteLine($"[gc-workload] Duration : {durationSeconds}s");
Console.WriteLine($"[gc-workload] Batch    : {rowsPerBatch} rows / cycle");
Console.WriteLine($"[gc-workload] PID      : {Environment.ProcessId}");

// One in-memory database, reused across cycles. The extract loop
// enumerates the seeded rows; the load loop bulk-inserts a fresh batch
// each iteration. Together they cover both hot paths of the library.
await using var conn = new SqliteConnection("Data Source=:memory:");
await conn.OpenAsync();

await using (var create = conn.CreateCommand())
{
    create.CommandText = @"
        CREATE TABLE widget (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL NOT NULL);
    ";
    await create.ExecuteNonQueryAsync();
}

// Seed 100k rows once so the extractor has something to enumerate.
await SeedAsync(conn, rowCount: 100_000);

var stopwatch = Stopwatch.StartNew();
long cycles = 0, extracted = 0, loaded = 0;

// SIGTERM handler so a workflow's timeout kills the process cleanly
// and we still print the summary.
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));

while (!cts.IsCancellationRequested)
{
    var extractor = new DbExtractor<Widget>(conn,
        "SELECT id AS Id, name AS Name, price AS Price FROM widget");
    await foreach (var _ in extractor.ExtractAsync(cts.Token).ConfigureAwait(false))
    {
        extracted++;
    }

    var loader = new DbLoader<Widget>(conn,
        "INSERT INTO widget (name, price) VALUES (@Name, @Price)");
    await loader.LoadAsync(GenerateBatch(rowsPerBatch), cts.Token).ConfigureAwait(false);
    loaded += rowsPerBatch;

    cycles++;
    if (cycles % 10 == 0)
    {
        var alloc = GC.GetTotalAllocatedBytes(precise: true);
        Console.WriteLine(
            $"[gc-workload] t={stopwatch.Elapsed.TotalSeconds,7:F1}s  " +
            $"cycles={cycles,5}  extracted={extracted,10}  loaded={loaded,10}  " +
            $"gen0={GC.CollectionCount(0),4}  gen1={GC.CollectionCount(1),3}  gen2={GC.CollectionCount(2),3}  " +
            $"total-alloc={alloc / 1024 / 1024,7} MB");
    }
}

Console.WriteLine();
Console.WriteLine($"[gc-workload] Completed {cycles} cycles in {stopwatch.Elapsed.TotalSeconds:F1}s.");
Console.WriteLine($"[gc-workload] Final GC counts: gen0={GC.CollectionCount(0)} gen1={GC.CollectionCount(1)} gen2={GC.CollectionCount(2)}");
Console.WriteLine($"[gc-workload] Peak working set: {Environment.WorkingSet / 1024 / 1024} MB");

static int ParseDuration(string[] args)
{
    if (args.Length > 0 && int.TryParse(args[0], out var s) && s > 0) return s;
    if (int.TryParse(Environment.GetEnvironmentVariable("GC_WORKLOAD_SECONDS"), out var envs) && envs > 0) return envs;
    return 600; // 10 minutes default per #142 AC.
}

static async Task SeedAsync(SqliteConnection conn, int rowCount)
{
    await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = "INSERT INTO widget (id, name, price) VALUES ($id, $name, $price)";
    var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; cmd.Parameters.Add(pId);
    var pName = cmd.CreateParameter(); pName.ParameterName = "$name"; cmd.Parameters.Add(pName);
    var pPrice = cmd.CreateParameter(); pPrice.ParameterName = "$price"; cmd.Parameters.Add(pPrice);

    for (var i = 1; i <= rowCount; i++)
    {
        pId.Value = i;
        pName.Value = $"widget-{i}";
        pPrice.Value = (i * 0.01) % 10.0;
        await cmd.ExecuteNonQueryAsync();
    }
    await tx.CommitAsync();
}

static async IAsyncEnumerable<Widget> GenerateBatch(int count)
{
    for (var i = 0; i < count; i++)
    {
        yield return new Widget { Name = $"batch-{i}", Price = i * 0.05m };
        if ((i & 0xFF) == 0)
        {
            await Task.Yield();
        }
    }
}

[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
internal sealed class Widget
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
