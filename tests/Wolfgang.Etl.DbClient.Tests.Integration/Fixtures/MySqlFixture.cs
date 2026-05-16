using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MySqlConnector;
using Testcontainers.MySql;

namespace Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;

[ExcludeFromCodeCoverage]
public sealed class MySqlFixture : DbProviderFixtureBase
{
    private MySqlContainer? _container;

    public override string ProviderName => "mysql";



    protected override async Task StartAsync()
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
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
        var conn = new MySqlConnection(_container!.GetConnectionString());
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }



    public override async Task ResetSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS contract_items;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "CREATE TABLE contract_items (name VARCHAR(100) NOT NULL, value INT NOT NULL);", cancellationToken).ConfigureAwait(false);
    }



    public override async Task SeedAsync(DbConnection connection, int rowCount, CancellationToken cancellationToken = default)
    {
        for (var i = 1; i <= rowCount; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO contract_items (name, value) VALUES (@name, @value);";
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = string.Format(CultureInfo.InvariantCulture, "Item{0}", i);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@value"; p2.Value = i * 10;
            cmd.Parameters.Add(p1);
            cmd.Parameters.Add(p2);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
