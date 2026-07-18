// Tests for DbSchemaValidator (#20).
//
// The validator's contract:
//   1. Missing [Table] → InvalidOperationException with the type FullName.
//   2. Missing table on DB → InvalidOperationException with the table name.
//   3. Missing column → InvalidOperationException naming BOTH the missing
//      columns AND the columns the table actually has (so a copy-paste
//      typo is immediately obvious).
//   4. All mapped columns present → no throw.
//   5. Async companion behaves identically + honours cancellation.
//   6. Connection state is restored (Closed → Closed) after Validate.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DbSchemaValidatorTests
{
    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("widget")]
    public sealed class Widget
    {
        [Key]
        [Column("id")] public int Id { get; set; }
        [Column("name")] public string Name { get; set; } = "";
        [Column("price")] public decimal Price { get; set; }
        [NotMapped] public string DisplayName { get; set; } = "";
    }

    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public sealed class WithoutTable
    {
        [Column("id")] public int Id { get; set; }
    }

    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("widget")]
    public sealed class WidgetWithExtraColumn
    {
        [Column("id")] public int Id { get; set; }
        [Column("name")] public string Name { get; set; } = "";
        [Column("price")] public decimal Price { get; set; }
        [Column("does_not_exist")] public string Ghost { get; set; } = "";
    }

    private static SqliteConnection CreateSeededConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE widget (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL NOT NULL);";
        cmd.ExecuteNonQuery();
        return conn;
    }

    [Fact]
    public void Validate_when_all_mapped_columns_exist_returns_normally()
    {
        using var conn = CreateSeededConnection();
        DbSchemaValidator.Validate<Widget>(conn);
    }

    [Fact]
    public void Validate_when_type_has_no_table_attribute_throws()
    {
        using var conn = CreateSeededConnection();
        var ex = Assert.Throws<InvalidOperationException>
        (
            () => DbSchemaValidator.Validate<WithoutTable>(conn)
        );
        Assert.Contains
        (
            "does not have a [Table] attribute",
            ex.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_when_table_missing_throws_with_table_name()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ex = Assert.Throws<InvalidOperationException>
        (
            () => DbSchemaValidator.Validate<Widget>(conn)
        );
        Assert.Contains
        (
            "table 'widget'",
            ex.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_when_column_missing_throws_listing_both_missing_and_actual()
    {
        using var conn = CreateSeededConnection();
        var ex = Assert.Throws<InvalidOperationException>
        (
            () => DbSchemaValidator.Validate<WidgetWithExtraColumn>(conn)
        );
        Assert.Contains
        (
            "'does_not_exist'",
            ex.Message,
            StringComparison.Ordinal
        );
        // Names of the actual columns are also listed so a typo is
        // immediately obvious.
        Assert.Contains
        (
            "name",
            ex.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_null_connection_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>
        (
            () => DbSchemaValidator.Validate<Widget>(null!)
        );
    }

    [Fact]
    public void Validate_leaves_already_open_connection_open()
    {
        // If the caller passed an already-open connection, the validator
        // must NOT close it — that would be a state-mutation the caller
        // didn't ask for. (The closed-connection-reopen path is
        // exercised by production consumers on real DBs; SQLite in-
        // memory can't test it because :memory: is per-connection and
        // Close destroys the database.)
        using var conn = CreateSeededConnection();
        Assert.Equal(ConnectionState.Open, conn.State);

        DbSchemaValidator.Validate<Widget>(conn);

        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    public async Task ValidateAsync_when_all_mapped_columns_exist_returns_normally()
    {
        await using var conn = CreateSeededConnection();
        await DbSchemaValidator.ValidateAsync<Widget>(conn);
    }

    [Fact]
    public async Task ValidateAsync_when_column_missing_throws()
    {
        await using var conn = CreateSeededConnection();
        await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => DbSchemaValidator.ValidateAsync<WidgetWithExtraColumn>(conn)
        );
    }

    [Fact]
    public async Task ValidateAsync_honours_cancellation()
    {
        await using var conn = CreateSeededConnection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>
        (
            () => DbSchemaValidator.ValidateAsync<Widget>(conn, cts.Token)
        );
    }
}
