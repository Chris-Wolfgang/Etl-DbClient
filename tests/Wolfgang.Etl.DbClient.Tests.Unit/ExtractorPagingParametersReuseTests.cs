using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Xunit;

// Parameters is the obsolete DynamicParameters setter, which is the path under test.
#pragma warning disable CS0618

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class ExtractorPagingParametersReuseTests
{
    private static DbExtractor<PersonRecord> CreatePage
    (
        System.Data.Common.DbConnection conn,
        DynamicParameters parameters,
        long offset
    )
    {
        return new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age > @min ORDER BY age",
            new Dictionary<string, object>(),
            new DbExtractorOptions
            {
                PagingClauseTemplate = PagingClauseTemplates.Sqlite,
                ServerOffset = offset,
                ServerLimit = 2,
            }
        )
        {
            Parameters = parameters,
        };
    }



    [Fact]
    public async Task ExtractAsync_when_server_paging_does_not_add_the_paging_parameters_to_the_callers_DynamicParameters()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(5);
        var parameters = new DynamicParameters();
        parameters.Add("@min", 0);

        var rows = await CreatePage(conn, parameters, offset: 0).ExtractAsync().ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "@min" }, parameters.ParameterNames.Select(n => "@" + n).ToArray());
    }



    [Fact]
    public async Task ExtractAsync_when_the_same_DynamicParameters_is_reused_for_the_next_page_returns_that_page()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(5);
        var parameters = new DynamicParameters();
        parameters.Add("@min", 0);

        var first = await CreatePage(conn, parameters, offset: 0).ExtractAsync().ToListAsync();
        var second = await CreatePage(conn, parameters, offset: 2).ExtractAsync().ToListAsync();

        Assert.Equal(new[] { "First1", "First2" }, first.Select(r => r.FirstName).ToArray());
        Assert.Equal(new[] { "First3", "First4" }, second.Select(r => r.FirstName).ToArray());
    }



    [Fact]
    public async Task ExtractAsync_when_one_extractor_is_enumerated_twice_with_server_paging_does_not_throw()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(5);
        var parameters = new DynamicParameters();
        parameters.Add("@min", 0);
        var sut = CreatePage(conn, parameters, offset: 0);

        var first = await sut.ExtractAsync().ToListAsync();
        var second = await sut.ExtractAsync().ToListAsync();

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
    }
}
