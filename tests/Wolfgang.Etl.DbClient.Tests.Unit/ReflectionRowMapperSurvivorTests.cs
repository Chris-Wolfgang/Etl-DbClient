using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Pins behaviour of <see cref="ReflectionRowMapper{TRecord}"/> and <see cref="DefaultValueTypes"/>
/// that the Stryker run on #474 reported as surviving mutants: read-only properties, the choice
/// between several matching constructors, the defaults passed for NULL columns, the single-character
/// conversion, and the text of each error the mapper raises.
/// </summary>
public class ReflectionRowMapperSurvivorTests
{
    [ExcludeFromCodeCoverage]
    public sealed class GetOnlyPropertyRecord
    {
        public long Id { get; set; }

        public string Fixed { get; } = "fixed";
    }



    [ExcludeFromCodeCoverage]
    public sealed class TwoConstructorRecord
    {
        public TwoConstructorRecord(long id)
        {
            Id = id;
            Name = "short";
        }



        public TwoConstructorRecord(long id, string name)
        {
            Id = id;
            Name = name;
        }



        public long Id { get; }

        public string Name { get; }
    }



    [ExcludeFromCodeCoverage]
    public sealed record BoolParameterRecord(bool Flag);



    private static async Task<List<T>> ReadAsync<T>(string sql)
    {
        using var conn = TestDb.CreateConnection();
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        using var reader = await command.ExecuteReaderAsync();
        var parse = ReflectionRowMapper<T>.CreateParser(reader);
        var rows = new List<T>();
        while (await reader.ReadAsync())
        {
            rows.Add(parse(reader));
        }

        return rows;
    }



    [Fact]
    public async Task CreateParser_when_a_column_names_a_get_only_property_leaves_it_unset()
    {
        var rows = await ReadAsync<GetOnlyPropertyRecord>("SELECT 7 AS Id, 'from db' AS Fixed");

        Assert.Equal("fixed", Assert.Single(rows).Fixed);
    }



    [Fact]
    public async Task CreateParser_when_several_constructors_match_uses_the_one_with_the_most_parameters()
    {
        var rows = await ReadAsync<TwoConstructorRecord>("SELECT 1 AS Id, 'long' AS Name");

        Assert.Equal("long", Assert.Single(rows).Name);
    }



    [Fact]
    public async Task CreateParser_when_a_null_column_targets_a_bool_parameter_passes_false()
    {
        var rows = await ReadAsync<BoolParameterRecord>("SELECT NULL AS Flag");

        Assert.False(Assert.Single(rows).Flag);
    }



    [Fact]
    public async Task CreateParser_when_no_constructor_matches_names_the_columns_in_the_message()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => ReadAsync<ReflectionRowMapperTests.NoMatchingConstructorRecord>("SELECT 1 AS Other, 2 AS Second")
        );

        Assert.Contains
        (
            "has no public parameterless constructor and no public constructor whose parameters all name a column of the result set (Other, Second).",
            ex.Message,
            StringComparison.Ordinal
        );
    }



    [Fact]
    public async Task CreateParser_when_a_value_cannot_be_converted_names_the_ordinal_value_and_target_type()
    {
        var ex = await Assert.ThrowsAsync<DataException>
        (
            () => ReadAsync<ReflectionRowMapperTests.PositionalRecord>("SELECT 'not a number' AS Id, 'n' AS Name, 1 AS Count")
        );

        Assert.Equal
        (
            "Error parsing column 0 (Id=not a number - System.String) as System.Int64",
            ex.Message
        );
    }



    [Fact]
    public async Task CreateParser_when_a_null_column_targets_an_unsupported_value_type_names_the_type()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>
        (
            () => ReadAsync<ReflectionRowMapperTests.KeyValuePairRecord>("SELECT NULL AS Pair")
        );

        Assert.Equal
        (
            $"A NULL column cannot be passed to a constructor parameter of type '{typeof(KeyValuePair<int, int>).FullName}'.",
            ex.Message
        );
    }



    [Fact]
    public void ConvertValue_when_target_is_a_guid_and_value_is_a_number_names_both_types()
    {
        var ex = Assert.Throws<InvalidCastException>
        (
            () => ReflectionRowMapper<ReflectionRowMapperTests.PositionalRecord>.ConvertValue(5L, typeof(Guid))
        );

        Assert.Equal("Cannot convert System.Int64 to System.Guid.", ex.Message);
    }



    [Fact]
    public void ConvertValue_when_a_one_character_string_targets_an_int_parses_the_number()
    {
        Assert.Equal(5, ReflectionRowMapper<ReflectionRowMapperTests.PositionalRecord>.ConvertValue("5", typeof(int)));
    }
}
