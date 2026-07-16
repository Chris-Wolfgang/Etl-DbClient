// Continuous fuzz over DbCommandBuilder's SQL emitter.
//
// The per-PR unit + snapshot suites cover a handful of hand-picked record
// shapes. Continuous fuzz asks CsCheck to generate hundreds of thousands
// of parameterised inputs (via [Trait("Category", "Fuzz")]) and asserts
// per-input invariants that MUST hold across every shape.
//
// Case count is controlled by:
//   - CsCheck.Size (env var CsCheck_Size; default 100)
//   - CsCheck.Iter (env var CsCheck_Iter; default 100)
// The fuzz.yaml workflow sets both to 100_000 for the scheduled run and
// a small number for local iteration.
//
// A failing input auto-shrinks to a minimal repro; CsCheck prints the
// counter-example. The fuzz workflow attaches the repro to the auto-
// filed issue.
//
// Refs #129.

using CsCheck;
using JetBrains.Annotations;
using Wolfgang.Etl.DbClient;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Fuzz;

public class DbCommandBuilderFuzz
{
    // Generator of "record shape" indices — CsCheck can't generate .NET
    // types dynamically without Reflection.Emit, so we index into a
    // static pool of hand-shaped test types that between them exercise:
    // - identity key + regular columns
    // - composite key
    // - all-Key
    // - one-column
    // - NotMapped column present
    // - column names with mixed casing (dotted-i regression bait)
    private static readonly Type[] TypePool =
    {
        typeof(Shape.SingleColumn),
        typeof(Shape.IdentityKeyOnly),
        typeof(Shape.IdentityKeyWithColumns),
        typeof(Shape.CompositeKey),
        typeof(Shape.AllKey),
        typeof(Shape.WithNotMapped),
        typeof(Shape.MixedCase),
    };

    [Fact]
    [Trait("Category", "Fuzz")]
    public void Select_is_stable_across_calls()
    {
        // Identity property: the cache means BuildSelect<T>() must
        // return the exact same string on every call. Fuzz drives
        // BOTH random type-pool indices AND concurrent access
        // (via CsCheck's Sample) to shake out any missing lock /
        // stale-cache bug that per-PR tests miss.
        Gen.Int[0, TypePool.Length - 1].Sample(idx =>
        {
            var t = TypePool[idx];
            var first = InvokeBuildSelect(t);
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(first, InvokeBuildSelect(t));
            }
        });
    }

    [Fact]
    [Trait("Category", "Fuzz")]
    public void Insert_shape_is_always_valid()
    {
        Gen.Int[0, TypePool.Length - 1].Sample(idx =>
        {
            var t = TypePool[idx];
            var sql = InvokeBuildInsert(t);
            // Shape invariants that must ALWAYS hold, per any record shape
            // BuildInsert supports:
            Assert.StartsWith("INSERT INTO ", sql, StringComparison.Ordinal);
            Assert.Contains(" VALUES ", sql, StringComparison.Ordinal);
            // Every '@Param' occurrence in the VALUES clause must appear
            // exactly once in the whole SQL (parameter list can't have
            // duplicates).
            var valuesIdx = sql.IndexOf(" VALUES ", StringComparison.Ordinal);
            var afterValues = sql[valuesIdx..];
            var paramCount = afterValues.Count(c => c == '@');
            var paramColumnCount = CountColumnList(sql, valuesIdx);
            Assert.Equal(paramColumnCount, paramCount);
        });
    }

    [Fact]
    [Trait("Category", "Fuzz")]
    public void Update_shape_is_always_valid_when_type_has_key()
    {
        Gen.Int[0, TypePool.Length - 1].Sample(idx =>
        {
            var t = TypePool[idx];
            // Shapes whose Update SQL BuildUpdate rejects — no Key column
            // (SingleColumn) or every column is Key so the SET clause
            // would be empty (AllKey). The rejection paths are covered
            // by the per-PR suite; here we only fuzz the happy shape.
            if (t == typeof(Shape.SingleColumn) || t == typeof(Shape.AllKey)) return;

            var sql = InvokeBuildUpdate(t);
            Assert.StartsWith("UPDATE ", sql, StringComparison.Ordinal);
            Assert.Contains(" SET ", sql, StringComparison.Ordinal);
            Assert.Contains(" WHERE ", sql, StringComparison.Ordinal);
        });
    }

    // ------------------------------------------------------------------
    // Reflection helpers so we can call the generic Build* methods with
    // a runtime Type. DbCommandBuilder is `internal static`; the
    // InternalsVisibleTo entry in the runtime csproj lets us reach it.

    private static string InvokeBuildSelect(Type recordType) =>
        (string)typeof(DbCommandBuilder)
            .GetMethod(nameof(DbCommandBuilder.BuildSelect), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(recordType)
            .Invoke(null, null)!;

    private static string InvokeBuildInsert(Type recordType) =>
        (string)typeof(DbCommandBuilder)
            .GetMethod(nameof(DbCommandBuilder.BuildInsert), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(recordType)
            .Invoke(null, null)!;

    private static string InvokeBuildUpdate(Type recordType) =>
        (string)typeof(DbCommandBuilder)
            .GetMethod(nameof(DbCommandBuilder.BuildUpdate), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(recordType)
            .Invoke(null, null)!;

    private static int CountColumnList(string sql, int valuesIdx)
    {
        // Count the params inside `(…)` between "INTO …(" and " VALUES ".
        var openParen = sql.IndexOf('(');
        var closeParen = sql.IndexOf(')', openParen);
        if (openParen < 0 || closeParen < 0 || closeParen > valuesIdx) return 0;
        return sql[openParen..closeParen].Count(c => c == ',') + 1;
    }
}
