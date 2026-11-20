using System;
using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DefaultTotalCountQueryCommandTypeTests
{
    private static DbExtractor<PersonRecord> CreateStoredProcedureExtractor(System.Data.Common.DbConnection conn)
    {
        return new DbExtractor<PersonRecord>
        (
            conn,
            "usp_GetPeople",
            new System.Collections.Generic.Dictionary<string, object>(),
            new DbExtractorOptions { CommandType = CommandType.StoredProcedure }
        );
    }



    [Fact]
    public async Task CountAsync_when_CommandType_is_StoredProcedure_and_no_TotalCountQuery_throws_a_clear_error()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var sut = CreateStoredProcedureExtractor(conn);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CountAsync());

        Assert.Contains("TotalCountQuery", ex.Message, StringComparison.Ordinal);
        Assert.Contains("StoredProcedure", ex.Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task CountAsync_when_CommandType_is_StoredProcedure_and_TotalCountQuery_is_supplied_uses_it()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var sut = CreateStoredProcedureExtractor(conn);
#pragma warning disable CS0618
        sut.TotalCountQuery = _ => Task.FromResult(42);
#pragma warning restore CS0618

        Assert.Equal(42, await sut.CountAsync());
    }
}
