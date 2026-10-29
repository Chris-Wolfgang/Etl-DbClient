using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Dapper;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.Abstractions;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Second batch of mutation-survivor tests for #474: the generated SQL text and the exception
/// messages of <c>DbCommandBuilder</c>, the strict-mapping message and the register-once guard
/// of <c>ColumnAttributeTypeMapper</c>, <see cref="EtlParameter"/>'s instance-of-type
/// short-circuit and conversion message, and the guard clauses of
/// <see cref="EtlPipelineDbClientExtensions"/>.
/// </summary>
/// <remarks>
/// The existing tests check fragments (<c>Assert.Contains</c>) or only the exception type, so
/// Stryker's string, conditional and statement mutations on these lines survived. Each test
/// here pins the exact observable result.
/// </remarks>
public class SqlBuilderAndGuardContractTests
{
    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("mixed")]
    public sealed class Mixed
    {
        [Column("mixed_id")] public int Id { get; set; }
        public string Name { get; set; } = "";
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("nothing")]
    public sealed class NothingMapped
    {
        [NotMapped] public int Id { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public sealed class NoTable
    {
        public int Id { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("only_unmapped_key")]
    public sealed class OnlyUnmappedKey
    {
        [Key, NotMapped] public int Id { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("unmapped_key")]
    public sealed class UnmappedKey
    {
        [Key, NotMapped] public int Id { get; set; }
        [Column("value")] public string Value { get; set; } = "";
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("keys_only")]
    public sealed class KeysOnly
    {
        [Key, Column("key_a")] public int KeyA { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public sealed class RegisterOnce
    {
        [Column("register_once_id")] public int Id { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    public sealed class Widget
    {
        public int Id { get; set; }
    }



    private static string NameOf<T>() => typeof(T).FullName!;



    // ------------------------------------------------------------------
    // DbCommandBuilder: generated SQL
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSelect_when_column_name_matches_property_emits_no_alias()
    {
        Assert.Equal
        (
            "SELECT mixed_id AS Id, Name FROM mixed",
            DbCommandBuilder.BuildSelect<Mixed>()
        );
    }



    [Fact]
    public void BuildSelect_when_every_property_is_NotMapped_is_exactly_select_star()
    {
        Assert.Equal
        (
            "SELECT * FROM nothing",
            DbCommandBuilder.BuildSelect<NothingMapped>()
        );
    }



    // ------------------------------------------------------------------
    // DbCommandBuilder: exception messages
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSelect_when_type_has_no_Table_attribute_message_is_exact()
    {
        var ex = Assert.Throws<InvalidOperationException>(DbCommandBuilder.BuildSelect<NoTable>);

        Assert.Equal
        (
            $"Type '{NameOf<NoTable>()}' does not have a [Table] attribute. " +
            "Auto-generated SQL requires [Table(\"name\")] on the record type, " +
            "or pass a custom command text to the constructor.",
            ex.Message
        );
    }



    [Fact]
    public void BuildInsert_when_every_property_is_NotMapped_message_is_exact()
    {
        var ex = Assert.Throws<InvalidOperationException>(DbCommandBuilder.BuildInsert<NothingMapped>);

        Assert.Equal
        (
            $"Type '{NameOf<NothingMapped>()}' has no mapped columns for INSERT. " +
            "Ensure at least one public instance property is not decorated with [NotMapped].",
            ex.Message
        );
    }



    [Fact]
    public void BuildUpdate_when_type_has_no_Key_message_is_exact()
    {
        var ex = Assert.Throws<InvalidOperationException>(DbCommandBuilder.BuildUpdate<Mixed>);

        Assert.Equal
        (
            $"Type '{NameOf<Mixed>()}' has no properties decorated with [Key]. " +
            "UPDATE requires at least one key property for the WHERE clause.",
            ex.Message
        );
    }



    [Fact]
    public void BuildUpdate_when_the_only_property_is_an_unmapped_Key_message_is_exact()
    {
        var ex = Assert.Throws<InvalidOperationException>(DbCommandBuilder.BuildUpdate<OnlyUnmappedKey>);

        Assert.Equal
        (
            $"Type '{NameOf<OnlyUnmappedKey>()}' has no mapped columns. " +
            "UPDATE requires at least one mapped property that is not decorated with [NotMapped].",
            ex.Message
        );
    }



    [Fact]
    public void BuildUpdate_when_every_Key_is_NotMapped_message_is_exact()
    {
        var ex = Assert.Throws<InvalidOperationException>(DbCommandBuilder.BuildUpdate<UnmappedKey>);

        Assert.Equal
        (
            $"Type '{NameOf<UnmappedKey>()}' has [Key] properties but none are mapped columns. " +
            "Ensure key properties are not decorated with [NotMapped].",
            ex.Message
        );
    }



    [Fact]
    public void BuildUpdate_when_every_column_is_a_Key_message_is_exact()
    {
        var ex = Assert.Throws<InvalidOperationException>(DbCommandBuilder.BuildUpdate<KeysOnly>);

        Assert.Equal
        (
            $"Type '{NameOf<KeysOnly>()}' has no non-key columns for the SET clause. " +
            "UPDATE requires at least one property that is not decorated with [Key].",
            ex.Message
        );
    }



    // ------------------------------------------------------------------
    // ColumnAttributeTypeMapper
    // ------------------------------------------------------------------

    [Fact]
    public void Register_when_called_twice_keeps_the_first_type_map()
    {
        ColumnAttributeTypeMapper.Register<RegisterOnce>();
        var first = SqlMapper.GetTypeMap(typeof(RegisterOnce));

        ColumnAttributeTypeMapper.Register<RegisterOnce>();

        Assert.Same(first, SqlMapper.GetTypeMap(typeof(RegisterOnce)));
    }



    [Fact]
    public async Task ExtractAsync_when_StrictColumnMapping_and_unmappable_column_message_is_exact()
    {
        // Process-wide static; restored in the finally (same pattern as ColumnAttributeTypeMapperTests).
        var previous = DbClientOptions.StrictColumnMapping;
        DbClientOptions.StrictColumnMapping = true;
        try
        {
            using var conn = new SqliteConnection("Data Source=:memory:");
            await conn.OpenAsync();
            var extractor = new DbExtractor<Mixed>(conn, "SELECT 1 AS mixed_id, 'n' AS Name, 2 AS ghost_col", options: null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>
            (
                () => extractor.ExtractAsync().ToListAsync().AsTask()
            );

            Assert.Equal
            (
                $"Result-set column 'ghost_col' does not map to any property on '{NameOf<Mixed>()}'. " +
                "Either add a property of that name, decorate an existing property with " +
                "[Column(\"ghost_col\")], or alias the column in your SELECT statement. " +
                "Set DbClientOptions.StrictColumnMapping = false to silently drop unmapped columns.",
                ex.Message
            );
        }
        finally
        {
            DbClientOptions.StrictColumnMapping = previous;
        }
    }



    // ------------------------------------------------------------------
    // EtlParameter.SetValue
    // ------------------------------------------------------------------

    [Fact]
    public void SetValue_when_value_is_an_instance_of_a_non_convertible_declared_type_stores_it_unchanged()
    {
        // Convert.ChangeType(int, typeof(IComparable)) throws, so this only passes because the
        // instance-of-type check returns before the conversion.
        var p = new EtlParameter<IComparable>();

        p.SetValue(5);

        Assert.Equal(5, p.Value);
    }



    [Fact]
    public void SetValue_when_conversion_is_impossible_message_is_exact()
    {
        var p = new EtlParameter<int>();

        var ex = Assert.Throws<InvalidOperationException>(() => p.SetValue("not a number"));

        Assert.Equal
        (
            "The provider returned 'System.String' for a parameter declared as 'System.Int32', " +
            "and the value could not be converted. Declare the parameter with the type the provider returns.",
            ex.Message
        );
    }



    // ------------------------------------------------------------------
    // EtlPipelineDbClientExtensions guard clauses
    // ------------------------------------------------------------------

    [Fact]
    public void DbExtractor_when_an_argument_is_null_names_it()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        var pipeline = EtlPipeline.Create();
        var extractor = new DbExtractor<Widget>(conn, "SELECT 1 AS Id", options: null);

        Assert.Equal
        (
            "connection",
            Assert.Throws<ArgumentNullException>(() => pipeline.DbExtractor<Widget>((DbConnection)null!, "SELECT 1")).ParamName
        );
        Assert.Equal
        (
            "commandText",
            Assert.Throws<ArgumentNullException>(() => pipeline.DbExtractor<Widget>(conn, (string)null!)).ParamName
        );
        Assert.Equal
        (
            "pipeline",
            Assert.Throws<ArgumentNullException>(() => ((EtlPipeline)null!).DbExtractor(extractor)).ParamName
        );
    }



    [Fact]
    public void DbLoader_when_an_argument_is_null_names_it()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        var stage = EtlPipeline
            .Create()
            .DbExtractor<Widget>(conn, "SELECT 1 AS Id");
        var loader = new DbLoader<Widget>(conn, "INSERT INTO t (Id) VALUES (@Id)", options: null);

        Assert.Equal
        (
            "connection",
            Assert.Throws<ArgumentNullException>(() => stage.DbLoader<Widget>((DbConnection)null!, "INSERT ...")).ParamName
        );
        Assert.Equal
        (
            "commandText",
            Assert.Throws<ArgumentNullException>(() => stage.DbLoader<Widget>(conn, (string)null!)).ParamName
        );
        Assert.Equal
        (
            "pipeline",
            Assert.Throws<ArgumentNullException>(() => ((IEtlPipeline<Widget>)null!).DbLoader(loader)).ParamName
        );
        Assert.Equal
        (
            "loader",
            Assert.Throws<ArgumentNullException>(() => stage.DbLoader((DbLoader<Widget>)null!)).ParamName
        );
    }
}
