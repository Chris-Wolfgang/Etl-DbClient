using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Dapper;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.ErrorPolicies;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// The Native AOT row-mapping path (<see cref="ReflectionRowMapper{TRecord}"/>). The
/// conformance tests read the same rows through Dapper's emitted parser and through the
/// reflection mapper and require identical records, so the two front-ends cannot drift.
/// </summary>
public class ReflectionRowMapperTests
{
    // ------------------------------------------------------------------
    // Sample record shapes
    // ------------------------------------------------------------------

    public enum Shade
    {
        None = 0,
        Red = 1,
        Green = 2,
    }



    [ExcludeFromCodeCoverage]
    public sealed record MixedRecord
    {
        [Column("id")]
        public int Id { get; init; }

        [Column("label")]
        public string? Label { get; init; }

        public decimal Price { get; init; }

        public long Big { get; init; }

        public bool Flag { get; init; }

        public double Ratio { get; init; }

        public int? Maybe { get; init; }

        public Shade Shade { get; init; }

        public string Untouched { get; init; } = "initial";
    }



    [ExcludeFromCodeCoverage]
    public sealed record PositionalRecord(long Id, string Name, long Count);



    [ExcludeFromCodeCoverage]
    public record struct PointRecord
    {
        public int X { get; set; }

        public int Y { get; set; }
    }



    [ExcludeFromCodeCoverage]
    public sealed class NoMatchingConstructorRecord
    {
        public NoMatchingConstructorRecord(string unrelated)
        {
            Unrelated = unrelated;
        }



        public string Unrelated { get; }
    }



    [ExcludeFromCodeCoverage]
    public sealed record GuidRecord(Guid Key, char Initial);



    [ExcludeFromCodeCoverage]
    public sealed record UnsupportedNullRecord(Version Version, int Count);



    // ------------------------------------------------------------------
    // Conformance: Dapper parser == reflection mapper
    // ------------------------------------------------------------------

    private const string MixedSql = @"
        SELECT 1 AS id, 'one' AS label, 1.25 AS Price, 9000000000 AS Big, 1 AS Flag, 0.5 AS Ratio, 7 AS Maybe, 2 AS Shade, NULL AS Untouched, 'x' AS Extra
        UNION ALL
        SELECT 2, NULL, 0.0, -1, 0, 2.0, NULL, 0, 'set', 'y'";



    [Fact]
    public async Task CreateParser_when_record_has_settable_properties_matches_Dapper()
    {
        await AssertConformsAsync<MixedRecord>(MixedSql);
    }



    [Fact]
    public async Task CreateParser_when_record_is_positional_matches_Dapper()
    {
        await AssertConformsAsync<PositionalRecord>("SELECT 5 AS Id, 'five' AS Name, 3 AS Count UNION ALL SELECT 6, 'six', 0");
    }



    [Fact]
    public async Task CreateParser_when_record_is_a_struct_matches_Dapper()
    {
        await AssertConformsAsync<PointRecord>("SELECT 3 AS X, 4 AS Y UNION ALL SELECT 0, NULL");
    }



    [Fact]
    public async Task CreateParser_when_record_is_a_scalar_matches_Dapper()
    {
        await AssertConformsAsync<long>("SELECT 10 UNION ALL SELECT 20");
        await AssertConformsAsync<long?>("SELECT 1 UNION ALL SELECT NULL");
    }



    [Fact]
    public async Task CreateParser_when_record_has_annotated_properties_matches_Dapper()
    {
        // PersonRecord has reference equality, so compare a projection of its values.
        const string sql = "SELECT 1 AS id, 'Ada' AS first_name, 'Lovelace' AS last_name, 36 AS age";
        ColumnAttributeTypeMapper.Register<PersonRecord>();

        var viaDapper = await ReadAsync(sql, reader => reader.GetRowParser<PersonRecord>());
        var viaMapper = await ReadWithMapperAsync<PersonRecord>(sql);

        Assert.Equal
        (
            viaDapper.Select(p => (p.Id, p.FirstName, p.LastName, p.Age)),
            viaMapper.Select(p => (p.Id, p.FirstName, p.LastName, p.Age))
        );
    }



    // ------------------------------------------------------------------
    // Mapper-only behaviour
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateParser_when_record_is_a_string_reads_the_first_column()
    {
        // Not a conformance test: Dapper's GetRowParser<string> throws InvalidCastException
        // (it builds a Func<DbDataReader, object>), while its Query<string> works.
        var rows = await ReadWithMapperAsync<string>("SELECT 'a', 'ignored' UNION ALL SELECT NULL, NULL");

        Assert.Equal(new[] { "a", null }, rows);
    }



    [Fact]
    public async Task CreateParser_when_scalar_column_is_null_returns_default()
    {
        var rows = await ReadWithMapperAsync<int>("SELECT NULL");

        Assert.Equal(0, Assert.Single(rows));
    }



    [Fact]
    public async Task CreateParser_when_text_columns_hold_a_guid_and_a_char_converts_them()
    {
        var key = Guid.NewGuid();

        var rows = await ReadWithMapperAsync<GuidRecord>($"SELECT '{key}' AS Key, 'Q' AS Initial");

        Assert.Equal(new GuidRecord(key, 'Q'), Assert.Single(rows));
    }



    [Fact]
    public async Task CreateParser_when_constructor_parameter_column_is_null_passes_default()
    {
        var rows = await ReadWithMapperAsync<PositionalRecord>("SELECT NULL AS Id, NULL AS Name, NULL AS Count");

        Assert.Equal(new PositionalRecord(0, null!, 0), Assert.Single(rows));
    }



    [Fact]
    public async Task CreateParser_when_no_constructor_matches_the_columns_throws_InvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => ReadWithMapperAsync<NoMatchingConstructorRecord>("SELECT 1 AS Other")
        );

        Assert.Contains(nameof(NoMatchingConstructorRecord), ex.Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task CreateParser_when_null_column_targets_an_unsupported_value_type_parameter_throws_NotSupportedException()
    {
        await Assert.ThrowsAsync<NotSupportedException>
        (
            () => ReadWithMapperAsync<KeyValuePairRecord>("SELECT NULL AS Pair")
        );
    }



    [Fact]
    public async Task CreateParser_when_value_cannot_be_converted_throws_DataException_naming_the_column()
    {
        var ex = await Assert.ThrowsAsync<DataException>
        (
            () => ReadWithMapperAsync<PositionalRecord>("SELECT 'not a number' AS Id, 'n' AS Name, 1 AS Count")
        );

        Assert.Contains("Id=not a number", ex.Message, StringComparison.Ordinal);
    }



    [Theory]
    [InlineData("green", Shade.Green)]
    [InlineData(1L, Shade.Red)]
    public void ConvertValue_when_target_is_an_enum_accepts_names_and_numbers(object value, Shade expected)
    {
        Assert.Equal(expected, ReflectionRowMapper<MixedRecord>.ConvertValue(value, typeof(Shade)));
    }



    [Fact]
    public void ConvertValue_when_target_is_a_guid_accepts_bytes()
    {
        var key = Guid.NewGuid();

        Assert.Equal(key, ReflectionRowMapper<MixedRecord>.ConvertValue(key.ToByteArray(), typeof(Guid)));
    }



    [Fact]
    public void ConvertValue_when_target_is_a_guid_and_value_is_a_number_throws_InvalidCastException()
    {
        Assert.Throws<InvalidCastException>
        (
            () => ReflectionRowMapper<MixedRecord>.ConvertValue(5L, typeof(Guid))
        );
    }



    [Fact]
    public void ConvertValue_when_target_is_a_nullable_converts_to_the_underlying_type()
    {
        Assert.Equal(42, ReflectionRowMapper<MixedRecord>.ConvertValue(42L, typeof(int?)));
    }



    [Fact]
    public async Task CreateParser_when_null_column_targets_an_enum_parameter_passes_the_zero_value()
    {
        var rows = await ReadWithMapperAsync<EnumParameterRecord>("SELECT NULL AS Shade");

        Assert.Equal(new EnumParameterRecord(Shade.None), Assert.Single(rows));
    }



    // ------------------------------------------------------------------
    // DbExtractor wiring
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExtractAsync_when_ForceReflectionRowMapper_extracts_the_same_records_as_Dapper()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(4);
        const string sql = "SELECT id, first_name, last_name, age FROM People ORDER BY id";

        var viaDapper = await new DbExtractor<PersonRecord>(conn, sql, new DbExtractorOptions()).ExtractAsync().ToListAsync();
        var viaMapper = await new DbExtractor<PersonRecord>(conn, sql, new DbExtractorOptions()) { ForceReflectionRowMapper = true }
            .ExtractAsync()
            .ToListAsync();

        Assert.Equal
        (
            viaDapper.Select(p => (p.Id, p.FirstName, p.LastName, p.Age)),
            viaMapper.Select(p => (p.Id, p.FirstName, p.LastName, p.Age))
        );
    }



    [Fact]
    public async Task ExtractAsync_when_ForceReflectionRowMapper_and_ErrorPolicy_is_Skip_skips_the_bad_row()
    {
        using var conn = TestDb.CreateConnection();
        using (var seed = conn.CreateCommand())
        {
            seed.CommandText = @"
                CREATE TABLE People (first_name TEXT, age INTEGER);
                INSERT INTO People (first_name, age) VALUES ('Ada', 30);
                INSERT INTO People (first_name, age) VALUES ('Bad', 'oops');
                INSERT INTO People (first_name, age) VALUES ('Zoe', 40);";
            await seed.ExecuteNonQueryAsync();
        }

        var extractor = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name AS FirstName, age AS Age FROM People",
            new DbExtractorOptions { ErrorPolicy = ItemErrorPolicy.Skip }
        )
        {
            ForceReflectionRowMapper = true,
        };

        var results = await extractor.ExtractAsync().ToListAsync();

        Assert.Equal(new[] { "Ada", "Zoe" }, results.Select(p => p.FirstName));
        Assert.Equal(1, extractor.CurrentErrorItemCount);
    }



    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    [ExcludeFromCodeCoverage]
    public sealed record KeyValuePairRecord(KeyValuePair<int, int> Pair);



    [ExcludeFromCodeCoverage]
    public sealed record EnumParameterRecord(Shade Shade);



    private static async Task AssertConformsAsync<T>(string sql)
    {
        // DbExtractor registers the [Column] type map in its static constructor; do the same
        // so Dapper resolves [Column] names here too.
        ColumnAttributeTypeMapper.Register<T>();
        var viaDapper = await ReadAsync(sql, reader => reader.GetRowParser<T>());
        var viaMapper = await ReadAsync(sql, ReflectionRowMapper<T>.CreateParser);

        Assert.Equal(viaDapper, viaMapper);
    }



    private static Task<List<T>> ReadWithMapperAsync<T>(string sql) =>
        ReadAsync(sql, ReflectionRowMapper<T>.CreateParser);



    private static async Task<List<T>> ReadAsync<T>(string sql, Func<DbDataReader, Func<DbDataReader, T>> createParser)
    {
        using var conn = TestDb.CreateConnection();
        using var command = conn.CreateCommand();
        command.CommandText = sql;
        using var reader = await command.ExecuteReaderAsync();
        var parse = createParser(reader);
        var rows = new List<T>();
        while (await reader.ReadAsync())
        {
            rows.Add(parse(reader));
        }

        return rows;
    }
}
