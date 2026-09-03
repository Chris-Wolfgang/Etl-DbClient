using System;
using System.Collections.Generic;
using System.Linq;
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
        // Filter to non-indexed string properties rather than casting blindly: if a
        // non-string or indexed public static member is added later, an unconditional
        // cast would throw inside the data generator and the whole theory would fail
        // to report which preset is actually wrong.
        foreach (var property in typeof(PagingClauseTemplates)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0))
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
    public void AllPresets_discovers_every_preset()
    {
        // The filter in AllPresets() could in principle exclude everything, which would leave
        // the theory above passing vacuously. Assert the exact set rather than a count plus a
        // couple of names: a count alone still passes if one preset disappears and an unrelated
        // string property takes its place, which is precisely the substitution worth catching.
        var discovered = AllPresets()
            .Select(row => (string)row[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var expected = new[]
        {
            nameof(PagingClauseTemplates.Db2),
            nameof(PagingClauseTemplates.MySql),
            nameof(PagingClauseTemplates.Oracle),
            nameof(PagingClauseTemplates.PostgreSql),
            nameof(PagingClauseTemplates.Sqlite),
            nameof(PagingClauseTemplates.SqlServer)
        }.OrderBy(name => name, StringComparer.Ordinal).ToList();

        // Adding a preset is a deliberate act, so updating this list is part of that act.
        Assert.Equal(expected, discovered);
    }



    [Fact]
    public void The_library_default_is_still_the_LimitOffset_form()
    {
        // DbExtractorOptions now initialises from PagingClauseTemplates.Sqlite, so comparing the
        // two would be tautological. Assert the literal shape instead: changing the Sqlite preset
        // silently changes the library-wide default, and that should be a deliberate act.
        Assert.Equal("LIMIT @PageLimit OFFSET @PageOffset", new DbExtractorOptions().PagingClauseTemplate);
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
