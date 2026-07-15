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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using VerifyTests;
using VerifyXunit;
using Wolfgang.Etl.DbClient;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Snapshots;

[ExcludeFromCodeCoverage]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
[Table("orders")]
internal sealed class OrderRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("total")]
    public decimal Total { get; set; }

    [Column("placed_utc")]
    public DateTime PlacedUtc { get; set; }

    // NotMapped: verifies the builder skips this column in SELECT / INSERT
    // / UPDATE without leaving artefacts in the generated SQL.
    [NotMapped]
    public string DisplayName { get; set; } = "";
}

[ExcludeFromCodeCoverage]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
[Table("order_lines")]
internal sealed class OrderLineRecord
{
    // Composite key: verifies the builder handles multi-column WHERE
    // clauses on UPDATE without regressing to single-key SQL.
    [Key]
    [Column("order_id")]
    public int OrderId { get; set; }

    [Key]
    [Column("line_no")]
    public int LineNo { get; set; }

    [Column("sku")]
    public string Sku { get; set; } = "";

    [Column("qty")]
    public int Qty { get; set; }
}

public class SqlSnapshotTests
{
    // Redirect Verify's snapshot files into a Snapshots/ subfolder to
    // match #140's AC. Configured once via ModuleInitializer.
    [ModuleInitializer]
    public static void Init() => Verifier.DerivePathInfo(
        (sourceFile, _, type, method) =>
            new PathInfo(
                directory: Path.Combine(Path.GetDirectoryName(sourceFile) ?? ".", "Snapshots"),
                typeName: type.Name,
                methodName: method.Name));

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
