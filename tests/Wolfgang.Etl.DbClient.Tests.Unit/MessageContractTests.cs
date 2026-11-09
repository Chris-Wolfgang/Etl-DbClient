using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Pins the exact text of the library's validation and schema-check exception messages.
/// </summary>
/// <remarks>
/// The messages are part of the diagnosable surface: they name the offender and say what to do
/// instead. The existing tests check only fragments, so Stryker's string mutations on these
/// messages survived (#474). Asserting the full text kills them.
/// </remarks>
public class MessageContractTests
{
    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("widget")]
    public sealed class Widget
    {
        [Column("id")] public int Id { get; set; }
        [Column("name")] public string Name { get; set; } = "";
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("widget")]
    public sealed class WidgetWithGhosts
    {
        [Column("id")] public int Id { get; set; }
        [Column("zeta_ghost")] public string Zeta { get; set; } = "";
        [Column("alpha_ghost")] public string Alpha { get; set; } = "";
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public sealed class NoTable
    {
        public int Id { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("widget")]
    public sealed class NothingMapped
    {
        [NotMapped] public int Id { get; set; }
    }



    private const string MissingColumnsMessage =
        "Schema validation failed for table 'widget': " +
        "the following mapped column(s) are missing from the database: 'zeta_ghost', 'alpha_ghost'. " +
        "Table has: b_col, id, name.";

    private const string MissingTablePrefix =
        "Schema validation failed for table 'widget': the table does not exist or cannot be queried. " +
        "Underlying provider error: ";



    private static SqliteConnection Seeded()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE widget (name TEXT, id INTEGER, b_col TEXT);";
        cmd.ExecuteNonQuery();
        return conn;
    }



    private static SqliteConnection Empty()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        return conn;
    }



    [Fact]
    public void Validate_when_type_has_no_Table_attribute_message_is_exact()
    {
        using var conn = Seeded();

        var ex = Assert.Throws<InvalidOperationException>(() => DbSchemaValidator.Validate<NoTable>(conn));

        Assert.Equal
        (
            $"Type '{typeof(NoTable).FullName}' does not have a [Table] attribute. " +
            "Schema validation requires an explicit table mapping.",
            ex.Message
        );
    }



    [Fact]
    public void Validate_when_every_property_is_NotMapped_message_is_exact()
    {
        using var conn = Seeded();

        var ex = Assert.Throws<InvalidOperationException>(() => DbSchemaValidator.Validate<NothingMapped>(conn));

        Assert.Equal
        (
            $"Type '{typeof(NothingMapped).FullName}' declares no mapped columns. " +
            "Every property is [NotMapped]; nothing to validate.",
            ex.Message
        );
    }



    [Fact]
    public void Validate_when_columns_are_missing_message_lists_missing_in_order_and_actual_sorted()
    {
        using var conn = Seeded();

        var ex = Assert.Throws<InvalidOperationException>(() => DbSchemaValidator.Validate<WidgetWithGhosts>(conn));

        Assert.Equal(MissingColumnsMessage, ex.Message);
    }



    [Fact]
    public async Task ValidateAsync_when_columns_are_missing_message_lists_missing_in_order_and_actual_sorted()
    {
        using var conn = Seeded();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => DbSchemaValidator.ValidateAsync<WidgetWithGhosts>(conn)
        );

        Assert.Equal(MissingColumnsMessage, ex.Message);
    }



    [Fact]
    public void Validate_when_table_is_missing_message_names_the_table_and_the_provider_error()
    {
        using var conn = Empty();

        var ex = Assert.Throws<InvalidOperationException>(() => DbSchemaValidator.Validate<Widget>(conn));

        Assert.Equal(MissingTablePrefix + ex.InnerException!.Message, ex.Message);
    }



    [Fact]
    public async Task ValidateAsync_when_table_is_missing_message_names_the_table_and_the_provider_error()
    {
        using var conn = Empty();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => DbSchemaValidator.ValidateAsync<Widget>(conn)
        );

        Assert.Equal(MissingTablePrefix + ex.InnerException!.Message, ex.Message);
    }



    [Fact]
    public void EtlParameterSet_Add_when_the_name_collides_message_is_exact()
    {
        // Two keys, only one colliding: the collision check must be "any key matches", not "all".
        var sut = new EtlParameterSet(new Dictionary<string, object> { ["@Status"] = "paid", ["@PageLimit"] = 5 });

        var ex = Assert.Throws<InvalidOperationException>(() => sut.Add("@PageLimit", 10));

        Assert.Equal
        (
            "Parameter '@PageLimit' was supplied in the parameters dictionary and is also " +
            "generated automatically, so it cannot be applied twice. Server-side paging " +
            "generates '@PageLimit' when ServerLimit is set. Either remove " +
            "'@PageLimit' from the dictionary and let paging supply it, or clear ServerLimit " +
            "and page through the command text yourself.",
            ex.Message
        );
    }



    [Fact]
    public void DbLoaderOptions_when_CommandTimeout_is_negative_message_is_exact()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>
        (
            () => _ = new DbLoaderOptions { CommandTimeout = TimeSpan.FromSeconds(-1) }
        );

        Assert.StartsWith
        (
            "CommandTimeout cannot be negative. Use null to fall back to the ADO.NET default.",
            ex.Message,
            StringComparison.Ordinal
        );
    }



    [Fact]
    public void DbLoaderOptions_when_InsertBatchSize_is_zero_message_is_exact()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DbLoaderOptions { InsertBatchSize = 0 });

        Assert.StartsWith("InsertBatchSize must be at least 1.", ex.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void DbLoaderOptions_when_MaxErrorCount_is_negative_message_is_exact()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DbLoaderOptions { MaxErrorCount = -1 });

        Assert.StartsWith("MaxErrorCount cannot be negative. Use 0 for unlimited.", ex.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void DbLoaderOptions_when_BatchCommitSize_is_negative_message_is_exact()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DbLoaderOptions { BatchCommitSize = -1 });

        Assert.StartsWith
        (
            "BatchCommitSize cannot be negative. Use 0 for 'commit only at end'.",
            ex.Message,
            StringComparison.Ordinal
        );
    }



    [Fact]
    public void DbLoaderOptions_when_BatchSize_is_zero_message_is_exact()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _ = new DbLoaderOptions { BatchSize = 0 });

        Assert.StartsWith("BatchSize must be at least 1.", ex.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void DbExtractorOptions_when_CommandTimeout_is_negative_message_is_exact()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>
        (
            () => _ = new DbExtractorOptions { CommandTimeout = TimeSpan.FromSeconds(-1) }
        );

        Assert.StartsWith
        (
            "CommandTimeout cannot be negative. Use null to fall back to the ADO.NET default.",
            ex.Message,
            StringComparison.Ordinal
        );
    }



    [Fact]
    public void DbExtractorOptions_when_CommandTimeout_is_zero_accepts_it()
    {
        var options = new DbExtractorOptions { CommandTimeout = TimeSpan.Zero };

        Assert.Equal(TimeSpan.Zero, options.CommandTimeout);
    }



    [Fact]
    public void Validate_when_connection_is_closed_opens_it_and_closes_it_again()
    {
        // A shared-cache in-memory database lives as long as one connection holds it open, so the
        // keeper owns the table while the validator opens and closes its own connection to it.
        var source = "Data Source=validate-closed-" + Guid.NewGuid().ToString("N") + ";Mode=Memory;Cache=Shared";
        using var keeper = new SqliteConnection(source);
        keeper.Open();
        using (var cmd = keeper.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE widget (id INTEGER, name TEXT);";
            cmd.ExecuteNonQuery();
        }

        using var conn = new SqliteConnection(source);

        DbSchemaValidator.Validate<Widget>(conn);

        Assert.Equal(System.Data.ConnectionState.Closed, conn.State);
    }



    [Fact]
    public async Task ValidateAsync_when_connection_is_closed_opens_it_and_closes_it_again()
    {
        var source = "Data Source=validate-closed-async-" + Guid.NewGuid().ToString("N") + ";Mode=Memory;Cache=Shared";
        using var keeper = new SqliteConnection(source);
        keeper.Open();
        using (var cmd = keeper.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE widget (id INTEGER, name TEXT);";
            cmd.ExecuteNonQuery();
        }

        using var conn = new SqliteConnection(source);

        await DbSchemaValidator.ValidateAsync<Widget>(conn);

        Assert.Equal(System.Data.ConnectionState.Closed, conn.State);
    }



    [Fact]
    public async Task ValidateAsync_when_connection_is_open_leaves_it_open()
    {
        using var conn = Seeded();

        await DbSchemaValidator.ValidateAsync<Widget>(conn);

        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }
}
