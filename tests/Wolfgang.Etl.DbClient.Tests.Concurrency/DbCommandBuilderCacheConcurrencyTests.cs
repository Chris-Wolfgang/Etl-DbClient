// Coyote systematic-testing driver for DbCommandBuilder's SQL cache.
//
// The cache is a ConcurrentDictionary<Type, TypeMetadata>. Concurrent
// consumers of the same TRecord race each other on first access — the
// invariant is: every thread ends up with the SAME SQL string (equal by
// value; ideally reference-equal via the cache). Coyote's scheduler
// explores thousands of interleavings of those concurrent calls,
// covering paths that a "run this test 10 000 times" stress test would
// never reproduce reliably.
//
// Coyote's engine works without IL rewriting for tests that use
// `TestingEngine.Create(config, action).Run()` directly — the engine
// takes control of the scheduler for the duration of `action`. That's
// what these xunit facts do. If we ever want the deeper systematic
// coverage that rewriting provides (every `await` intercepted, not
// just the ones inside our test action), a follow-up can add a
// coyote-rewrite step to the CI build pipeline.
//
// Refs #137.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using Wolfgang.Etl.DbClient;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Concurrency;

[ExcludeFromCodeCoverage]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
[Table("concurrent_probe")]
internal sealed class ConcurrentProbe
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("value")]
    public string Value { get; set; } = "";
}

public class DbCommandBuilderCacheConcurrencyTests
{
    // Iteration budget for the schedule-exploration engine. Low on PR,
    // higher on the scheduled workflow (env override picked up below).
    private static int Iterations =>
        int.TryParse(Environment.GetEnvironmentVariable("COYOTE_ITERATIONS"), out var n) && n > 0
            ? n
            : 100;

    /// <summary>
    /// Under any interleaving of N concurrent BuildSelect callers on the
    /// same TRecord, every caller must observe the same SQL string.
    /// A missing lock / race in the cache would surface as one caller
    /// getting a partially-built or stale-metadata version.
    /// </summary>
    [Fact]
    [Trait("Category", "Concurrency")]
    public void BuildSelect_is_race_free_under_concurrent_access()
    {
        RunUnderCoyote(() =>
        {
            const int workers = 4;
            var results = new string[workers];
            var tasks = new Task[workers];
            for (var i = 0; i < workers; i++)
            {
                var idx = i;
                tasks[i] = Task.Run(() =>
                {
                    results[idx] = DbCommandBuilder.BuildSelect<ConcurrentProbe>();
                });
            }
            Task.WaitAll(tasks);

            // All workers must have received the same string. Reference
            // equality would be stronger (proves cache hit), but string
            // interning could confuse it — value equality is enough for
            // the race-free invariant.
            var first = results[0];
            for (var i = 1; i < workers; i++)
            {
                Microsoft.Coyote.Specifications.Specification.Assert(
                    string.Equals(results[i], first, StringComparison.Ordinal),
                    "Worker {0} observed a different SELECT SQL than worker 0: '{1}' vs '{2}'.",
                    i, results[i], first);
            }
        });
    }

    /// <summary>
    /// Same invariant, INSERT flavour. Insert has a separate lazy under
    /// TypeMetadata so its race path is distinct from Select's.
    /// </summary>
    [Fact]
    [Trait("Category", "Concurrency")]
    public void BuildInsert_is_race_free_under_concurrent_access()
    {
        RunUnderCoyote(() =>
        {
            const int workers = 4;
            var results = new string[workers];
            var tasks = new Task[workers];
            for (var i = 0; i < workers; i++)
            {
                var idx = i;
                tasks[i] = Task.Run(() =>
                {
                    results[idx] = DbCommandBuilder.BuildInsert<ConcurrentProbe>();
                });
            }
            Task.WaitAll(tasks);

            var first = results[0];
            for (var i = 1; i < workers; i++)
            {
                Microsoft.Coyote.Specifications.Specification.Assert(
                    string.Equals(results[i], first, StringComparison.Ordinal),
                    "Worker {0} observed a different INSERT SQL than worker 0.",
                    i);
            }
        });
    }

    // ------------------------------------------------------------------

    private static void RunUnderCoyote(Action body)
    {
        var config = Configuration.Create()
            .WithTestingIterations((uint)Iterations)
            .WithMaxSchedulingSteps(500)
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
