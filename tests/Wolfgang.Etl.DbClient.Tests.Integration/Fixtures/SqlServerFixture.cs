using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;

[ExcludeFromCodeCoverage]
public sealed class SqlServerFixture : DbProviderFixtureBase
{
    private MsSqlContainer? _container;

    public override string ProviderName => "sqlserver";



    protected override async Task StartAsync()
    {
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _container.StartAsync().ConfigureAwait(false);
    }



    protected override async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }



    public override async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var conn = new SqlConnection(_container!.GetConnectionString());
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }



    public override async Task ResetSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(connection, "IF OBJECT_ID('dbo.contract_items','U') IS NOT NULL DROP TABLE dbo.contract_items;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "CREATE TABLE dbo.contract_items (name NVARCHAR(100) NOT NULL, value INT NOT NULL);", cancellationToken).ConfigureAwait(false);
    }



    public override async Task SeedAsync(DbConnection connection, int rowCount, CancellationToken cancellationToken = default)
    {
        for (var i = 1; i <= rowCount; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO dbo.contract_items (name, value) VALUES (@name, @value);";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = string.Format(CultureInfo.InvariantCulture, "Item{0}", i);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@value"; p2.Value = i * 10;
            cmd.Parameters.Add(p1);
            cmd.Parameters.Add(p2);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
