using System;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace Wolfgang.Etl.DbClient.Benchmarks;

/// <summary>
/// Per-RDBMS connection + DDL helper for the benchmark suite. The provider is
/// selected at process start via the <c>ETL_DBCLIENT_BENCHMARK_RDBMS</c> env var
/// (one of <c>sqlite</c>, <c>sqlserver</c>, <c>postgres</c>, <c>mysql</c>; defaults
/// to <c>sqlite</c>). Connection strings come from these env vars:
/// <list type="bullet">
///   <item><description><c>ETL_DBCLIENT_BENCHMARK_MSSQL</c></description></item>
///   <item><description><c>ETL_DBCLIENT_BENCHMARK_PG</c></description></item>
///   <item><description><c>ETL_DBCLIENT_BENCHMARK_MYSQL</c></description></item>
/// </list>
/// </summary>
public static class BenchmarkContext
{
    public static string Rdbms => (Environment.GetEnvironmentVariable("ETL_DBCLIENT_BENCHMARK_RDBMS") ?? "sqlite").Trim().ToLowerInvariant();



    public static DbConnection OpenConnection()
    {
        DbConnection conn = Rdbms switch
        {
            "sqlserver" => new SqlConnection(Require("ETL_DBCLIENT_BENCHMARK_MSSQL")),
            "postgres"  => new NpgsqlConnection(Require("ETL_DBCLIENT_BENCHMARK_PG")),
            "mysql"     => new MySqlConnection(Require("ETL_DBCLIENT_BENCHMARK_MYSQL")),
            "sqlite"    => new SqliteConnection("Data Source=:memory:"),
            _           => throw new InvalidOperationException($"Unknown ETL_DBCLIENT_BENCHMARK_RDBMS value '{Rdbms}'. Expected one of: sqlite, sqlserver, postgres, mysql."),
        };

        conn.Open();
        return conn;
    }



    public static async Task ResetSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));

        var (drop, create) = Rdbms switch
        {
            "sqlserver" => ("IF OBJECT_ID('dbo.contract_items','U') IS NOT NULL DROP TABLE dbo.contract_items;",
                            "CREATE TABLE dbo.contract_items (name NVARCHAR(100) NOT NULL, value INT NOT NULL);"),
            "postgres"  => ("DROP TABLE IF EXISTS contract_items;",
                            "CREATE TABLE contract_items (name VARCHAR(100) NOT NULL, value INTEGER NOT NULL);"),
            "mysql"     => ("DROP TABLE IF EXISTS contract_items;",
                            "CREATE TABLE contract_items (name VARCHAR(100) NOT NULL, value INT NOT NULL);"),
            _           => ("DROP TABLE IF EXISTS contract_items;",
                            "CREATE TABLE contract_items (name TEXT NOT NULL, value INTEGER NOT NULL);"),
        };

        await ExecuteAsync(connection, drop, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, create, cancellationToken).ConfigureAwait(false);
    }



    public static async Task SeedAsync(DbConnection connection, int rowCount, CancellationToken cancellationToken = default)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));

        var paramPrefix = string.Equals(Rdbms, "postgres", StringComparison.Ordinal) ? "" : "@";

        for (var i = 1; i <= rowCount; i++)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"INSERT INTO contract_items (name, value) VALUES ({paramPrefix}name, {paramPrefix}value);";

            var p1 = cmd.CreateParameter();
            p1.ParameterName = $"{paramPrefix}name";
            p1.Value = string.Format(CultureInfo.InvariantCulture, "Item{0}", i);
            cmd.Parameters.Add(p1);

            var p2 = cmd.CreateParameter();
            p2.ParameterName = $"{paramPrefix}value";
            p2.Value = i * 10;
            cmd.Parameters.Add(p2);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }



    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }



    private static string Require(string envVar)
    {
        var v = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(v))
        {
            throw new InvalidOperationException
            (
                $"Environment variable '{envVar}' is required when ETL_DBCLIENT_BENCHMARK_RDBMS='{Rdbms}'."
            );
        }
        return v;
    }
}
