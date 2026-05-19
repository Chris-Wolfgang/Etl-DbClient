using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Wolfgang.Etl.Abstractions;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Tests covering the public-surface behaviour of <c>ColumnAttributeTypeMapper</c>
/// — exercised indirectly via <see cref="DbExtractor{T}"/>.
/// </summary>
public class ColumnAttributeTypeMapperTests
{
    [ExcludeFromCodeCoverage]
    [Table("People")]
    public class AnnotatedRecord
    {
        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;
    }



    [Fact]
    public async Task ExtractAsync_when_result_set_has_unmappable_column_throws_descriptive_error()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(2);

        // The query SELECTs an alias ('age_in_years') that maps to NO property on
        // AnnotatedRecord — neither by name nor by [Column] attribute. The custom
        // type-map callback should throw an InvalidOperationException whose message
        // names the offending column and the target type, instead of leaking a
        // generic NullReferenceException from Dapper.
        var extractor = new DbExtractor<AnnotatedRecord>
        (
            conn,
            "SELECT first_name, last_name, age AS age_in_years FROM People"
        );

        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => extractor.ExtractAsync().ToListAsync().AsTask()
        );

        Assert.Contains("age_in_years", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(AnnotatedRecord), ex.Message, StringComparison.Ordinal);
    }
}
