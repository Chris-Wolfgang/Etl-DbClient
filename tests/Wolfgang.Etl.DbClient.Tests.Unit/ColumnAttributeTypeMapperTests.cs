using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
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
    public async Task ExtractAsync_when_StrictColumnMapping_and_unmappable_column_throws_descriptive_error()
    {
        // DbClientOptions.StrictColumnMapping is a process-wide static; restore
        // the previous value in a finally so this test can't leak into others.
        var previous = DbClientOptions.StrictColumnMapping;
        DbClientOptions.StrictColumnMapping = true;
        try
        {
            using var conn = await TestDb.CreateConnectionWithDataAsync(2);

            // The query SELECTs an alias ('age_in_years') that maps to NO property
            // on AnnotatedRecord — neither by name nor by [Column] attribute. With
            // StrictColumnMapping on, the type-map callback should throw an
            // InvalidOperationException whose message names the offending column
            // and the target type.
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
        finally
        {
            DbClientOptions.StrictColumnMapping = previous;
        }
    }



    [Fact]
    public async Task ExtractAsync_with_default_options_silently_drops_unmappable_column()
    {
        // Default (DbClientOptions.StrictColumnMapping == false): unmappable
        // columns should be ignored, matching Dapper's pre-existing behavior.
        // Restore the previous value defensively in case some other test left
        // strict mode on.
        var previous = DbClientOptions.StrictColumnMapping;
        DbClientOptions.StrictColumnMapping = false;
        try
        {
            using var conn = await TestDb.CreateConnectionWithDataAsync(2);

            var extractor = new DbExtractor<AnnotatedRecord>
            (
                conn,
                "SELECT first_name, last_name, age AS age_in_years FROM People"
            );

            // Should not throw — the extra column is dropped.
            var results = await extractor.ExtractAsync().ToListAsync();

            Assert.Equal(2, results.Count);
        }
        finally
        {
            DbClientOptions.StrictColumnMapping = previous;
        }
    }
}
