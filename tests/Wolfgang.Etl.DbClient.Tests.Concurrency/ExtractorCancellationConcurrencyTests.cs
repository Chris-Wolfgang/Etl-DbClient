// Extractor cancellation-race property.
//
// The interesting concurrent scenario for DbExtractor is: consumer starts
// iterating, cancellation token fires concurrently with the enumeration.
// The invariants that must hold under ANY scheduler interleaving:
//
//   1. Iteration terminates. Either the consumer reaches the end (all
//      rows delivered) OR OperationCanceledException propagates.
//   2. On cancellation, CurrentItemCount matches the number of rows the
//      consumer actually observed. No off-by-one where the counter
//      increments but the row wasn't handed to the consumer, or vice
//      versa.
//   3. The connection (opened by the extractor's ManageConnection code
//      path) is closed / disposed even when cancellation aborts the
//      iteration mid-flight — otherwise long-running consumers leak
//      connections.
//
// Refs #137.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
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
[Table("cancel_probe")]
internal sealed class CancelProbe
{
    [Column("id")]
    public int Id { get; set; }

    [Column("value")]
    public string Value { get; set; } = "";
}

public class ExtractorCancellationConcurrencyTests
{
    private static int Iterations =>
        int.TryParse(Environment.GetEnvironmentVariable("COYOTE_ITERATIONS"), out var n) && n > 0
            ? n
            : 50;

    /// <summary>
    /// Under any interleaving of iteration progress and cancellation
    /// fire, the iterator either completes normally or throws
    /// <see cref="OperationCanceledException"/>. Row count on the
    /// extractor never exceeds the number of rows the consumer
    /// observed.
    /// </summary>
    [Fact]
    [Trait("Category", "Concurrency")]
    public void ExtractAsync_terminates_and_counts_match_under_cancellation_race()
    {
        RunUnderCoyote(() =>
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            using (var seed = conn.CreateCommand())
            {
                seed.CommandText =
                    "CREATE TABLE cancel_probe (id INTEGER PRIMARY KEY, value TEXT NOT NULL); " +
                    "INSERT INTO cancel_probe (id, value) VALUES (1,'a'), (2,'b'), (3,'c'), (4,'d'), (5,'e');";
                seed.ExecuteNonQuery();
            }

            var extractor = new DbExtractor<CancelProbe>(
                conn,
                "SELECT id AS Id, value AS Value FROM cancel_probe ORDER BY id");

            using var cts = new CancellationTokenSource();
            var observed = 0;

            // Fire cancellation concurrently. Coyote's scheduler decides
            // when the cancel Task and the enumeration Task advance
            // relative to each other, so it explores fire-before-first-
            // row / fire-mid-row / fire-after-last-row all in the same
            // exploration campaign.
            var cancelTask = Task.Run(() =>
            {
                cts.Cancel();
            });

            var enumerateTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var _ in extractor.ExtractAsync(cts.Token).ConfigureAwait(false))
                    {
                        observed++;
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected — cancellation is a valid termination path
                }
            });

            Task.WaitAll(cancelTask, enumerateTask);

            // Invariant: extractor's own counter never exceeds what the
            // consumer observed. If the extractor incremented the count
            // BUT threw before yielding, that's a bug — a downstream
            // metric would over-report progress.
            Microsoft.Coyote.Specifications.Specification.Assert(
                extractor.CurrentItemCount <= observed,
                "Extractor CurrentItemCount ({0}) exceeded observed rows ({1}) — count/yield ordering bug.",
                extractor.CurrentItemCount, observed);
        });
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
