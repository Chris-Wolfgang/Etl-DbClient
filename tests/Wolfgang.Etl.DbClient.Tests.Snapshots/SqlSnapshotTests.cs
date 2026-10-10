// Snapshot tests over DbCommandBuilder's SQL emitter.
//
// Targeted unit tests already assert the shape of individual clauses; these
// lock in the WHOLE STRING for a set of representative record shapes so a
// refactor that accidentally changes formatting (extra whitespace, reordered
// parameter names, changed quoting style) fails the PR with a visible diff.
//
// First run of any test writes a `<name>.received.txt` alongside the
// `<name>.verified.txt` snapshot. Review the .received file, and if the
// change is intentional, replace the .verified file with it. The pre-
// commit and CI runs both fail if a `.received.txt` is present.
//
// Snapshot files land under tests/.../Snapshots/ per the #140 AC.
//
// Refs #140.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using VerifyTests;
using VerifyXunit;
using Wolfgang.Etl.DbClient;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Snapshots;

// The record shapes below are abstract: DbCommandBuilder reads their metadata
// through the type alone and never creates an instance, so abstract properties
// leave no accessor bodies sitting unexecuted.

[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
[Table("orders")]
internal abstract class OrderRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public abstract int Id { get; set; }

    [Column("customer_id")]
    public abstract int CustomerId { get; set; }

    [Column("total")]
    public abstract decimal Total { get; set; }

    [Column("placed_utc")]
    public abstract DateTime PlacedUtc { get; set; }

    // NotMapped: verifies the builder skips this column in SELECT / INSERT
    // / UPDATE without leaving artefacts in the generated SQL.
    [NotMapped]
    public abstract string DisplayName { get; set; }
}

[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
[Table("order_lines")]
internal abstract class OrderLineRecord
{
    // Composite key: verifies the builder handles multi-column WHERE
    // clauses on UPDATE without regressing to single-key SQL.
    [Key]
    [Column("order_id")]
    public abstract int OrderId { get; set; }

    [Key]
    [Column("line_no")]
    public abstract int LineNo { get; set; }

    [Column("sku")]
    public abstract string Sku { get; set; }

    [Column("qty")]
    public abstract int Qty { get; set; }
}

public class SqlSnapshotTests
{
    // Redirect Verify's snapshot files into a Snapshots/ subfolder to
    // match #140's AC. Configured once via ModuleInitializer.
    //
    // Internal (not public) so xUnit1013 doesn't flag it as an unmarked
    // test method. [ModuleInitializer] works with any accessibility
    // as long as the method is static, returns void, and takes no args.
    //
    // Deliberately ignores the `sourceFile` callback parameter (Verify's own
    // [CallerFilePath] capture) and resolves the project directory from
    // AppContext.BaseDirectory instead. CI builds set ContinuousIntegrationBuild
    // + <PathMap> (#255, cross-OS reproducibility), which rewrites embedded
    // source paths to a fictional `/_/...` root — `sourceFile` would resolve to
    // that non-existent path in CI, and Directory.CreateDirectory would fail
    // with UnauthorizedAccessException trying to create `/_` under filesystem
    // root. AppContext.BaseDirectory reflects the real runtime output
    // directory and is never rewritten by PathMap (that only touches
    // compile-time embedded literals).
    [ModuleInitializer]
    internal static void Init() => Verifier.DerivePathInfo(
        (_, _, type, method) =>
            new PathInfo(
                directory: Path.Combine(ResolveProjectDirectory(AppContext.BaseDirectory), "Snapshots"),
                typeName: type.Name,
                methodName: method.Name));



    internal static string ResolveProjectDirectory(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir != null; dir = dir.Parent)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException
        (
            $"Could not locate the Tests.Snapshots project directory by walking up from '{startDirectory}'."
        );
    }



    [Fact]
    public void ResolveProjectDirectory_when_no_ancestor_has_a_csproj_throws_InvalidOperationException()
    {
        var start = Path.Combine(Path.GetTempPath(), "dbclient-snapshots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(start);

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ResolveProjectDirectory(start));

            Assert.Contains(start, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(start);
        }
    }



    [Fact]
    public Task Select_orders() => Verifier.Verify(DbCommandBuilder.BuildSelect<OrderRecord>());

    [Fact]
    public Task Insert_orders_skips_identity_and_notmapped() =>
        Verifier.Verify(DbCommandBuilder.BuildInsert<OrderRecord>());

    [Fact]
    public Task Update_orders_where_by_identity_key() =>
        Verifier.Verify(DbCommandBuilder.BuildUpdate<OrderRecord>());

    [Fact]
    public Task Select_order_lines_composite_key() =>
        Verifier.Verify(DbCommandBuilder.BuildSelect<OrderLineRecord>());

    [Fact]
    public Task Update_order_lines_where_by_composite_key() =>
        Verifier.Verify(DbCommandBuilder.BuildUpdate<OrderLineRecord>());
}
