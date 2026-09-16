using System;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.Abstractions;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// The options records derive from the Abstractions base records (ADR-0009), so the settings every
/// extractor or loader shares are configured on the record and applied by the constructor.
/// </summary>
public class DbOptionsRecordTests
{
    private const string Select = "SELECT first_name AS FirstName FROM People";
    private const string Insert = "INSERT INTO People (first_name) VALUES (@FirstName)";

    private static SqliteConnection Connection() => new("Data Source=:memory:");

    private static Func<ItemErrorContext, ItemErrorAction> AnyPolicy => _ => default;



    [Fact]
    public void DbExtractorOptions_derives_from_ExtractorOptions()
    {
        Assert.IsAssignableFrom<ExtractorOptions>(new DbExtractorOptions());
    }



    [Fact]
    public void DbLoaderOptions_derives_from_LoaderOptions()
    {
        Assert.IsAssignableFrom<LoaderOptions>(new DbLoaderOptions());
    }



    [Fact]
    public void DbExtractor_when_constructed_with_options_applies_the_inherited_settings()
    {
        using var conn = Connection();
        var policy = AnyPolicy;
        var options = new DbExtractorOptions
        {
            ReportingInterval = 5,
            SkipItemCount = 2,
            MaximumItemCount = 3,
            ErrorPolicy = policy,
        };

        var sut = new DbExtractor<PersonRecord>(conn, Select, options);

        Assert.Equal(5, sut.ReportingInterval);
        Assert.Equal(2, sut.SkipItemCount);
        Assert.Equal(3, sut.MaximumItemCount);
        Assert.Same(policy, sut.ErrorPolicy);
    }



    [Fact]
    public void DbExtractor_when_constructed_without_options_matches_an_empty_record()
    {
        using var conn = Connection();

        var without = new DbExtractor<PersonRecord>(conn, Select, (DbExtractorOptions?)null);
        var empty = new DbExtractor<PersonRecord>(conn, Select, new DbExtractorOptions());

        Assert.Equal(empty.ReportingInterval, without.ReportingInterval);
        Assert.Equal(empty.SkipItemCount, without.SkipItemCount);
        Assert.Equal(empty.MaximumItemCount, without.MaximumItemCount);
    }



    [Fact]
    public void DbLoader_when_constructed_with_options_applies_the_inherited_settings_and_IsDryRun()
    {
        using var conn = Connection();
        var policy = AnyPolicy;
        var options = new DbLoaderOptions
        {
            ReportingInterval = 5,
            SkipItemCount = 2,
            MaximumItemCount = 3,
            ErrorPolicy = policy,
            IsDryRun = true,
        };

        var sut = new DbLoader<PersonRecord>(conn, Insert, options);

        Assert.Equal(5, sut.ReportingInterval);
        Assert.Equal(2, sut.SkipItemCount);
        Assert.Equal(3, sut.MaximumItemCount);
        Assert.Same(policy, sut.ErrorPolicy);
        Assert.True(sut.IsDryRun);
    }



    [Fact]
    public void DbLoader_when_constructed_without_options_matches_an_empty_record()
    {
        using var conn = Connection();

        var without = new DbLoader<PersonRecord>(conn, Insert, (DbLoaderOptions?)null);
        var empty = new DbLoader<PersonRecord>(conn, Insert, new DbLoaderOptions());

        Assert.Equal(empty.ReportingInterval, without.ReportingInterval);
        Assert.Equal(empty.SkipItemCount, without.SkipItemCount);
        Assert.Equal(empty.MaximumItemCount, without.MaximumItemCount);
        Assert.False(without.IsDryRun);
    }
}
