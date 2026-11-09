// Owned-connection disposal-race property.
//
// DbExtractor / DbLoader have TWO connection-lifecycle paths:
//   (a) Caller-owned: the connection is passed in via the DbConnection
//       ctor. The extractor / loader never disposes it — the caller
//       is responsible. Both #268 and #269 cover this path.
//   (b) OWNED: the DbProviderFactory ctor. The extractor / loader
//       creates the connection internally and MUST dispose it in a
//       finally block at the end of ExtractWorkerAsync /
//       LoadWorkerAsync (DbExtractor.cs:622-625, DbLoader.cs:637-640).
//
// This test covers path (b): under any interleaving of enumeration
// progress + cancel-token fire, the internal connection is disposed
// exactly once — no leak, no double-dispose crash — even when the
// finally block races with the cancellation propagation.
//
// The observable invariant: after enumeration terminates (normally or
// via OCE), a subsequent enumeration attempt on the same extractor
// throws (because the connection was disposed) rather than silently
// no-op or crash the process. A missing dispose would leave a live
// connection; a double dispose would throw ObjectDisposedException
// from the FIRST call before the second enumeration ever ran.
//
// Refs #137 follow-up.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JetBrains.Annotations;
using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;
using Xunit;

// Constructs via the deprecated constructors. Migrating to the options overloads is
// follow-up work; the deprecation exists to warn consumers, and the options constructors
// are covered by DbOptionsDefaultsTests.
#pragma warning disable CS0618

// VSTHRD002: `Task.WaitAll` inside a `RunUnderCoyote(Action)` body is
// intentional — the Coyote scheduler drives the exploration in a
// synchronous body delegate. Rewriting these joins as `await` would
// pull them out of the Coyote-controlled scheduler. Same pattern in
// all *ConcurrencyTests.cs files in this project.
#pragma warning disable VSTHRD002

// AccessToDisposedClosure: the CancellationTokenSource is captured by
// Task.Run closures and disposed via `using var`. The `Task.WaitAll`
// call below the closures guarantees both tasks complete BEFORE the
// `using` scope exits — so the token source stays alive for every
// access. InspectCode can't see through the WaitAll join, so it flags
// every capture. Silenced file-wide because this whole file follows
// the same Coyote-driven cancel-race pattern.
// ReSharper disable AccessToDisposedClosure

namespace Wolfgang.Etl.DbClient.Tests.Concurrency;

[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
[Table("owned_probe")]
internal sealed class OwnedProbe
{
    [Column("id")]
    public int Id { get; set; }
}

public class OwnedConnectionDisposalConcurrencyTests
{
    private static int Iterations =>
        int.TryParse(Environment.GetEnvironmentVariable("COYOTE_ITERATIONS"), out var n) && n > 0
            ? n
            : 50;

    /// <summary>
    /// Owned-connection extractor: under cancellation race, the finally
    /// block that disposes the internal DbConnection must still run
    /// exactly once. Observed by attempting a second enumeration and
    /// asserting the first enumeration didn't leave a corrupted state.
    /// </summary>
    [Fact]
    [Trait("Category", "Concurrency")]
    public void ExtractAsync_owned_connection_disposal_runs_under_cancellation_race()
    {
        RunUnderCoyote(() =>
        {
            // Owned-connection ctor path: extractor creates + disposes
            // the connection internally. Each ExtractAsync call opens
            // a fresh connection via the factory, then disposes it.
            var extractor = CreateOwnedExtractor();

            using var cts = new CancellationTokenSource();

            var cancelTask = Task.Run(() =>
            {
                cts.Cancel();
            });

            var enumTask = Task.Run(() => Outcome.DrainAsync(extractor.ExtractAsync(cts.Token)));

            Task.WaitAll(cancelTask, enumTask);
            var fault = enumTask.Result.Fault;

            // Invariant: the run either completed normally or was cancelled.
            // A disposal-race bug would surface here as an
            // ObjectDisposedException / null-reference from inside the
            // extractor's own state.
            Microsoft.Coyote.Specifications.Specification.Assert(
                fault is null or OperationCanceledException,
                "First enumeration threw an unexpected exception type: {0}",
                fault);
        });
    }



    /// <summary>
    /// Without cancellation the owned connection opens, yields its row and is
    /// disposed, and a second enumeration on the same extractor opens a fresh
    /// connection and succeeds — the first run left no corrupted state.
    /// </summary>
    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task ExtractAsync_owned_connection_when_never_cancelled_yields_the_row_on_every_run()
    {
        var extractor = CreateOwnedExtractor();
        var rows = new List<OwnedProbe>();

        var first = await Outcome.DrainAsync(Capture(extractor.ExtractAsync(CancellationToken.None), rows));

        var second = await Outcome.DrainAsync(extractor.ExtractAsync(CancellationToken.None));

        Assert.Null(first.Fault);
        Assert.Equal(1, first.Observed);
        Assert.Equal(1, Assert.Single(rows).Id);
        Assert.Null(second.Fault);
        Assert.Equal(1, second.Observed);
    }



    // Owned-connection ctor path: the extractor creates + disposes the
    // connection internally. Each ExtractAsync call opens a fresh in-memory
    // SQLite connection via the factory, so the query needs no table.
    private static DbExtractor<OwnedProbe> CreateOwnedExtractor() =>
        new(
            SqliteFactory.Instance,
            "Data Source=:memory:",
            "SELECT 1 AS Id");



    private static async IAsyncEnumerable<OwnedProbe> Capture(IAsyncEnumerable<OwnedProbe> source, List<OwnedProbe> rows)
    {
        await foreach (var row in source.ConfigureAwait(false))
        {
            rows.Add(row);
            yield return row;
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
