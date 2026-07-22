// Loader cancellation-race property — mirror of ExtractorCancellation-
// ConcurrencyTests, applied to the loader side of the library.
//
// DbLoader consumes an IAsyncEnumerable<TRecord> and writes each item to
// the database via ExecuteAsync. The cancellation invariants:
//
//   1. Under any interleaving of source-enumeration progress + cancel
//      token fire, LoadAsync terminates. Either the whole batch loads
//      OR OperationCanceledException propagates.
//   2. CurrentItemCount on the loader never exceeds the number of items
//      that were actually committed to the database. If the loader
//      increments the counter but throws before ExecuteAsync completes,
//      downstream metrics over-report the write.
//   3. When cancellation aborts mid-batch, previously-committed rows
//      (in the caller's own transaction, or in auto-commit mode)
//      remain persisted — nothing rolls back rows that were already
//      written in earlier iterations.
//
// Refs #137 (Coyote follow-up scope on #265 / #268).

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Concurrency;

[ExcludeFromCodeCoverage]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
internal sealed class LoadProbe
{
    public string Value { get; set; } = "";
}

public class LoaderCancellationConcurrencyTests
{
    private static int Iterations =>
        int.TryParse(Environment.GetEnvironmentVariable("COYOTE_ITERATIONS"), out var n) && n > 0
            ? n
            : 50;

    /// <summary>
    /// LoadAsync terminates cleanly under any interleaving of source
    /// enumeration + cancel-token fire, and CurrentItemCount matches
    /// the actual number of rows written to the destination table.
    /// </summary>
    [Fact]
    [Trait("Category", "Concurrency")]
    public void LoadAsync_terminates_and_counts_match_under_cancellation_race()
    {
        RunUnderCoyote(() =>
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var create = conn.CreateCommand())
            {
                create.CommandText = "CREATE TABLE load_probe (value TEXT NOT NULL);";
                create.ExecuteNonQuery();
            }

            var loader = new DbLoader<LoadProbe>(
                conn,
                "INSERT INTO load_probe (value) VALUES (@Value)");

            using var cts = new CancellationTokenSource();

            var cancelTask = Task.Run(() =>
            {
                cts.Cancel();
            });

            var loadTask = Task.Run(async () =>
            {
                try
                {
                    await loader.LoadAsync(GenerateAsync(5, cts.Token), cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // expected — cancellation is a valid termination path
                }
            });

            Task.WaitAll(cancelTask, loadTask);

            // Count what actually landed in the destination.
            int actualRows;
            using (var count = conn.CreateCommand())
            {
                count.CommandText = "SELECT COUNT(*) FROM load_probe;";
                actualRows = Convert.ToInt32(count.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }

            // Invariant: the loader's own count matches what's in the DB.
            // If they diverge, a downstream progress metric would over-
            // or under-report the actual write.
            Microsoft.Coyote.Specifications.Specification.Assert(
                loader.CurrentItemCount == actualRows,
                "Loader CurrentItemCount ({0}) does not match rows in destination ({1}).",
                loader.CurrentItemCount, actualRows);
        });
    }

    private static async IAsyncEnumerable<LoadProbe> GenerateAsync(
        int count,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return new LoadProbe { Value = $"item-{i}" };
            await Task.Yield();
        }
    }

    // ------------------------------------------------------------------

    private static void RunUnderCoyote(Action body)
    {
        var config = Configuration.Create()
            .WithTestingIterations((uint)Iterations)
            .WithMaxSchedulingSteps(1000)
            .WithVerbosityEnabled(Microsoft.Coyote.Logging.VerbosityLevel.Info);

        using var engine = TestingEngine.Create(config, body);
        engine.Run();

        var report = engine.TestReport;
        Assert.True(
            report.NumOfFoundBugs == 0,
            $"Coyote found {report.NumOfFoundBugs} bug(s). " +
            $"First: {(report.BugReports.Count > 0 ? report.BugReports.First() : "(no repro)")}");
    }
}
