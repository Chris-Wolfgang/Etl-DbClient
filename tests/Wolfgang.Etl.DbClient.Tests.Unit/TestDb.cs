using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

[ExcludeFromCodeCoverage]
internal static class TestDb
{
    internal static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }



    internal static async Task<SqliteConnection> CreateConnectionWithDataAsync(int rowCount = 5)
    {
        var connection = CreateConnection();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE People (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                first_name TEXT NOT NULL,
                last_name TEXT NOT NULL,
                age INTEGER NOT NULL
            )";
        await cmd.ExecuteNonQueryAsync();

        for (var i = 1; i <= rowCount; i++)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO People (first_name, last_name, age) VALUES (@fn, @ln, @age)";
            insert.Parameters.AddWithValue("@fn", $"First{i}");
            insert.Parameters.AddWithValue("@ln", $"Last{i}");
            insert.Parameters.AddWithValue("@age", 20 + i);
            await insert.ExecuteNonQueryAsync();
        }

        return connection;
    }



    internal static async Task CreateEmptyTableAsync(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS People (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                first_name TEXT NOT NULL,
                last_name TEXT NOT NULL,
                age INTEGER NOT NULL
            )";
        await cmd.ExecuteNonQueryAsync();
    }



    internal static async Task<int> CountRowsAsync(DbConnection connection, string table = "People")
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
        var result = await cmd.ExecuteScalarAsync();
        return System.Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }



    internal static SqliteConnection CreateContractConnection(int rowCount)
    {
        var connection = CreateConnection();

        using var createCmd = connection.CreateCommand();
        createCmd.CommandText = @"
            CREATE TABLE ContractItems (
                Name TEXT NOT NULL,
                Value INTEGER NOT NULL
            )";
        createCmd.ExecuteNonQuery();

        for (var i = 0; i < rowCount; i++)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO ContractItems (Name, Value) VALUES (@name, @value)";
            insert.Parameters.AddWithValue("@name", $"Item{i + 1}");
            insert.Parameters.AddWithValue("@value", (i + 1) * 10);
            insert.ExecuteNonQuery();
        }

        return connection;
    }



    internal static SqliteConnection CreateContractLoaderConnection()
    {
        var connection = CreateConnection();

        using var createCmd = connection.CreateCommand();
        createCmd.CommandText = @"
            CREATE TABLE ContractItems (
                Name TEXT NOT NULL,
                Value INTEGER NOT NULL
            )";
        createCmd.ExecuteNonQuery();

        return connection;
    }
}
