using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;

namespace Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;

/// <summary>
/// CockroachDB fixture. Speaks the PostgreSQL wire protocol so the existing
/// <c>Npgsql</c> driver works unchanged — only the container image differs.
/// Testcontainers does not ship a dedicated <c>Testcontainers.CockroachDb</c>
/// module, so this fixture uses the generic <see cref="ContainerBuilder"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CockroachDbFixture : DbProviderFixtureBase
{
    private const int CockroachSqlPort = 26257;
    private const int CockroachHttpPort = 8080;

    private IContainer? _container;

    public override string ProviderName => "cockroachdb";



    protected override async Task StartAsync()
    {
        // Pinned patch tag for reproducible CI. Bump deliberately when
        // moving to a new minor.
        _container = new ContainerBuilder()
            .WithImage("cockroachdb/cockroach:v24.3.5")
            .WithCommand("start-single-node", "--insecure")
            .WithPortBinding(CockroachSqlPort, true)
            .WithPortBinding(CockroachHttpPort, true)
            // Wait by actually executing a SQL probe inside the container —
            // the SQL port opens a few seconds before the engine is accepting
            // queries, and the HTTP /health endpoint's behaviour varies across
            // versions. A successful `SELECT 1` is the unambiguous signal.
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("./cockroach", "sql", "--insecure", "-e", "SELECT 1"))
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
        var port = _container!.GetMappedPublicPort(CockroachSqlPort);
        // Insecure single-node defaults: user 'root', no password, database 'defaultdb'.
        var connectionString = $"Host=localhost;Port={port};Database=defaultdb;Username=root;";
        var conn = new NpgsqlConnection(connectionString);
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
            var p1 = cmd.CreateParameter(); p1.ParameterName = "name"; p1.Value = string.Format(CultureInfo.InvariantCulture, "Item{0}", i);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "value"; p2.Value = i * 10;
            cmd.Parameters.Add(p1);
            cmd.Parameters.Add(p2);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
