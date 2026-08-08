// Allocation-free hot-path gate.
//
// DbCommandBuilder caches per-type SQL strings after the first build. That
// makes BuildSelect / BuildInsert / BuildUpdate zero-allocation ON THE
// SECOND-AND-BEYOND CALL for a given TRecord. A regression that
// accidentally rebuilds the SQL on every call (e.g. someone drops the
// cache lookup, or replaces the TypeMetadataCache with per-call
// computation) would silently blow the perf-critical extractor + loader
// paths — those hit these methods once per row.
//
// Documented no-alloc list (as-of the sha this file is committed at):
//   * DbCommandBuilder.BuildSelect<T>()
//   * DbCommandBuilder.BuildInsert<T>()
//   * DbCommandBuilder.BuildUpdate<T>()
// Anything else in the public surface may allocate freely — DbClient's
// hot path outside the SQL cache is bound by Dapper's parameter binding
// and ADO.NET's own allocations, neither of which we control.
//
// If you add a new no-alloc-by-contract method, add a `[Fact]` here and
// a bullet to the list above. Refs #147.

#if NET5_0_OR_GREATER || NETCOREAPP3_1
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Wolfgang.Etl.DbClient;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class AllocationTests
{
    // Local no-alloc record, isolated from PersonRecord so a schema tweak
    // on shared fixtures can't invalidate the allocation budget.
    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("allocation_probe")]
    internal sealed class AllocationProbe
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = "";

        [Column("total")]
        public decimal Total { get; set; }
    }

    // Number of cache-hit invocations to measure over. Larger multiplier
    // makes any per-call allocation regression trivial to see (a 40-byte
    // regression shows as 4000 bytes over 100 iterations).
    private const int IterationCount = 100;

    [Fact]
    public void BuildSelect_is_zero_alloc_after_warmup()
        => AssertZeroAlloc(() => DbCommandBuilder.BuildSelect<AllocationProbe>());

    [Fact]
    public void BuildInsert_is_zero_alloc_after_warmup()
        => AssertZeroAlloc(() => DbCommandBuilder.BuildInsert<AllocationProbe>());

    [Fact]
    public void BuildUpdate_is_zero_alloc_after_warmup()
        => AssertZeroAlloc(() => DbCommandBuilder.BuildUpdate<AllocationProbe>());

    // Warm the cache + JIT with a handful of calls, then measure
    // GC.GetAllocatedBytesForCurrentThread() before and after
    // IterationCount cache-hit calls. Delta must be exactly 0.
    private static void AssertZeroAlloc(Func<string> hotPath)
    {
        // Warm up: prime the cache + JIT tiering. Two rounds so tier-1
        // recompilation (if any) also runs before the measurement.
        for (var i = 0; i < 8; i++)
        {
            _ = hotPath();
        }

        // Suppress a Gen-2 GC exactly on the boundary — Gen-2 collection
        // increments the allocated-bytes counter as a side effect of the
        // finalizer thread. Force one BEFORE the measurement so the
        // window is clean.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < IterationCount; i++)
        {
            _ = hotPath();
        }
        var after = GC.GetAllocatedBytesForCurrentThread();

        var delta = after - before;
        Assert.True(
            delta == 0,
            $"Expected zero allocations over {IterationCount} cache-hit calls; got {delta} bytes " +
            $"({delta / IterationCount} bytes/call). Regression — the SQL cache is no longer being hit on every call.");
    }
}
#endif
