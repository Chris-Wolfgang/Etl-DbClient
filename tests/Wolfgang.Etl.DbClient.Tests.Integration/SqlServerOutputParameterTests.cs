using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration;

/// <summary>
/// Output / InputOutput <see cref="EtlParameter{T}"/> values must reach the caller after
/// <c>ExtractAsync</c>. SQLite rejects output parameters, so only a server provider can prove it.
/// </summary>
[Collection("SqlServer")]
[Trait("Category", "sqlserver")]
public sealed class SqlServerOutputParameterTests
{
    private readonly SqlServerFixture _fixture;

    public SqlServerOutputParameterTests(SqlServerFixture fixture) => _fixture = fixture;



    [SkippableFact]
    public async Task ExtractAsync_writes_Output_and_InputOutput_EtlParameter_values_back()
    {
        Skip.IfNot(_fixture.Available, _fixture.UnavailableReason);

        using var conn = await _fixture.OpenConnectionAsync();
        await _fixture.ResetSchemaAsync(conn);
        await _fixture.SeedAsync(conn, rowCount: 3);

        using (var create = conn.CreateCommand())
        {
            create.CommandText =
                "CREATE OR ALTER PROCEDURE dbo.usp_items_with_total @total INT OUTPUT, @running INT OUTPUT AS " +
                "BEGIN SET @total = (SELECT COUNT(*) FROM dbo.contract_items); SET @running = @running + 100; " +
                "SELECT name AS Name, value AS Value FROM dbo.contract_items ORDER BY value; END";
            await create.ExecuteNonQueryAsync();
        }

        var total = new EtlParameter<int> { Direction = ParameterDirection.Output, DbType = DbType.Int32 };
        var running = new EtlParameter<int> { Value = 5, Direction = ParameterDirection.InputOutput, DbType = DbType.Int32 };

        var sut = new DbExtractor<ContractItem>
        (
            conn,
            "dbo.usp_items_with_total",
            new Dictionary<string, object> { ["@total"] = total, ["@running"] = running },
            new DbExtractorOptions { CommandType = CommandType.StoredProcedure }
        );

        var rows = await sut.ExtractAsync().ToListAsync();

        Assert.Equal(3, rows.Count);
        Assert.Equal(3, total.Value);
        Assert.Equal(105, running.Value);
    }
}
