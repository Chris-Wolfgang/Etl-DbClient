using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Wolfgang.Etl.Ado.Tests.Unit;

// ------------------------------------------------------------------
// Test record types
// ------------------------------------------------------------------

[ExcludeFromCodeCoverage]
[Table("People")]
public class PersonRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }



    [Column("first_name")]
    public string FirstName { get; set; } = string.Empty;



    [Column("last_name")]
    public string LastName { get; set; } = string.Empty;



    [Column("age")]
    public int Age { get; set; }
}



[ExcludeFromCodeCoverage]
public class SimpleRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}



// ------------------------------------------------------------------
// Database fixture
// ------------------------------------------------------------------

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
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        for (var i = 1; i <= rowCount; i++)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO People (first_name, last_name, age) VALUES (@fn, @ln, @age)";
            insert.Parameters.AddWithValue("@fn", $"First{i}");
            insert.Parameters.AddWithValue("@ln", $"Last{i}");
            insert.Parameters.AddWithValue("@age", 20 + i);
            await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
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
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }



    internal static async Task<int> CountRowsAsync(DbConnection connection, string table = "People")
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return Convert.ToInt32(result);
    }
}
