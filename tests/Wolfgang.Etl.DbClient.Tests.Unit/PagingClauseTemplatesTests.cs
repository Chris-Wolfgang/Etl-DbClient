using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Guards the paging presets. A typo in one of these surfaces only as a runtime SQL error on a
/// database that may not be exercised locally, so it is worth asserting cheaply here.
/// </summary>
public class PagingClauseTemplatesTests
{
    public static IEnumerable<object[]> AllPresets()
    {
        foreach (var property in typeof(PagingClauseTemplates)
            .GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            yield return new object[] { property.Name, (string)property.GetValue(null)! };
        }
    }


    [Theory]
    [MemberData(nameof(AllPresets))]
    public void Every_preset_references_both_generated_parameter_names(string name, string template)
    {
        // Reflection rather than a hand-listed set: a preset added later is covered automatically,
        // which is the case most likely to be forgotten.
        Assert.False(string.IsNullOrWhiteSpace(template), $"{name} is empty");
        Assert.Contains("@PageOffset", template, StringComparison.Ordinal);
        Assert.Contains("@PageLimit", template, StringComparison.Ordinal);
    }


    [Fact]
    public void The_library_default_matches_the_Sqlite_preset()
    {
        // The documented default is the LIMIT/OFFSET form; if one drifts from the other the docs
        // become wrong silently.
        Assert.Equal(PagingClauseTemplates.Sqlite, new DbExtractorOptions().PagingClauseTemplate);
    }


    [Fact]
    public void The_OffsetFetch_presets_agree_with_each_other()
    {
        Assert.Equal(PagingClauseTemplates.SqlServer, PagingClauseTemplates.Oracle);
        Assert.Equal(PagingClauseTemplates.SqlServer, PagingClauseTemplates.Db2);
    }


    [Fact]
    public void SqlServer_does_not_use_LIMIT()
    {
        // The specific mistake this class exists to prevent.
        Assert.DoesNotContain("LIMIT", PagingClauseTemplates.SqlServer, StringComparison.Ordinal);
    }
}
