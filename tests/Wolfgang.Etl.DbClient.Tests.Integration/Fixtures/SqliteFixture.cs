using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;

/// <summary>
/// SQLite "integration" fixture — uses an in-memory shared-cache database so
/// SQLite gets the same matrix row treatment as the container-backed providers.
/// No Docker required, so this fixture is always <c>Available</c>.
/// </summary>
[ExcludeFromCodeCoverage]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Lifetime is managed by xunit via IAsyncLifetime.DisposeAsync, which disposes _holdOpen in StopAsync.")]
public sealed class SqliteFixture : DbProviderFixtureBase
{
    private string _connectionString = string.Empty;
    private SqliteConnection? _holdOpen;

    public override string ProviderName => "sqlite";



    protected override Task StartAsync()
    {
        // shared-cache + a named in-memory DB lets multiple connections see the
        // same data; the "hold open" connection keeps the DB alive between tests.
        _connectionString = $"Data Source=file:integration-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _holdOpen = new SqliteConnection(_connectionString);
        _holdOpen.Open();
        return Task.CompletedTask;
    }



    protected override Task StopAsync()
    {
        _holdOpen?.Dispose();
        _holdOpen = null;
        return Task.CompletedTask;
    }



    public override async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        return conn;
    }



    public override async Task ResetSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS contract_items;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "CREATE TABLE contract_items (name TEXT NOT NULL, value INTEGER NOT NULL);", cancellationToken).ConfigureAwait(false);
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
