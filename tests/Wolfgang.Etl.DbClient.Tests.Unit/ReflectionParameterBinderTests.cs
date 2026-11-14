using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// The Native AOT parameter-binding path of <see cref="DbLoader{TRecord}"/>. The conformance
/// tests load the same records through Dapper's emitted binder and through the reflection
/// binder and require identical rows in the database, so the two cannot drift.
/// </summary>
public class ReflectionParameterBinderTests
{
    private const string InsertSql =
        "INSERT INTO bind (id, name, shade, maybe, price, flag) VALUES (@Id, @Name, @Shade, @Maybe, @Price, @Flag)";



    public enum Shade
    {
        None = 0,
        Red = 1,
        Green = 2,
    }



    [ExcludeFromCodeCoverage]
    public sealed record BindRecord
    {
        public long Id { get; init; }

        public string? Name { get; init; }

        public Shade Shade { get; init; }

        public int? Maybe { get; init; }

        public decimal Price { get; init; }

        public bool Flag { get; init; }

        public string NotInSql { get; init; } = "unused";
    }



    private static readonly BindRecord[] Records =
    [
        new() { Id = 1, Name = "sprocket", Shade = Shade.Red, Maybe = 7, Price = 1.99m, Flag = true },
        new() { Id = 2, Name = null, Shade = Shade.None, Maybe = null, Price = 0m, Flag = false },
        new() { Id = 3, Name = "grommet", Shade = Shade.Green, Maybe = -1, Price = 1234.5m, Flag = true },
        new() { Id = 4, Name = "O'Brien", Shade = Shade.Red, Maybe = 0, Price = -0.25m, Flag = false },
        new() { Id = 5, Name = "", Shade = Shade.Green, Maybe = int.MaxValue, Price = 10m, Flag = true },
    ];



    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task LoadAsync_when_ForceReflectionParameterBinder_writes_the_same_rows_as_Dapper(int batchSize)
    {
        var viaDapper = await LoadAndReadBackAsync(forceReflection: false, batchSize);
        var viaReflection = await LoadAndReadBackAsync(forceReflection: true, batchSize);

        Assert.Equal(Records.Length, viaReflection.Count);
        Assert.Equal
        (
            viaDapper,
            viaReflection
        );
    }



    [Fact]
    public async Task LoadAsync_when_ForceReflectionParameterBinder_and_ErrorHandling_is_Skip_skips_the_failing_row()
    {
        using var connection = await CreateTableAsync();
        var loader = new DbLoader<BindRecord>
        (
            connection,
            InsertSql,
            new DbLoaderOptions { ErrorHandling = RowErrorHandling.Skip }
        )
        {
            ForceReflectionParameterBinder = true,
        };

        // Id is the primary key, so the repeated 1 violates it and is skipped.
        await loader.LoadAsync(ToAsyncEnumerable([Records[0], Records[0] with { Name = "dupe" }, Records[1]]));

        Assert.Equal(2, loader.CurrentItemCount);
        Assert.Equal(1, loader.CurrentErrorCount);
    }



    [Fact]
    public void CreateReflectionParameterBinder_for_Text_binds_only_the_properties_the_SQL_names()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var loader = new DbLoader<BindRecord>(connection, InsertSql, new DbLoaderOptions());

        Assert.Equal
        (
            ["Id", "Name", "Shade", "Maybe", "Price", "Flag"],
            loader.CreateReflectionParameterBinder().Names
        );
    }



    [Fact]
    public void CreateReflectionParameterBinder_for_StoredProcedure_binds_every_readable_property()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var loader = new DbLoader<BindRecord>
        (
            connection,
            "usp_insert_bind",
            new DbLoaderOptions { CommandType = CommandType.StoredProcedure }
        );

        Assert.Equal
        (
            ["Id", "Name", "Shade", "Maybe", "Price", "Flag", "NotInSql"],
            loader.CreateReflectionParameterBinder().Names
        );
    }



    [Theory]
    [InlineData("VALUES (@Id)", "Id", true)]
    [InlineData("VALUES (@id)", "Id", true)]
    [InlineData("VALUES (:Id)", "Id", true)]
    [InlineData("VALUES (?Id)", "Id", true)]
    [InlineData("@Id", "Id", true)]
    [InlineData("VALUES (Id)", "Id", false)]
    [InlineData("VALUES (@IdX)", "Id", false)]
    [InlineData("VALUES (@Id_2)", "Id", false)]
    [InlineData("VALUES (@Id2)", "Id", false)]
    [InlineData("VALUES (@OrderId, @Order)", "Order", true)]
    [InlineData("VALUES (@OrderId)", "Order", false)]
    [InlineData("WHERE x = 'Id'", "Id", false)]
    [InlineData("", "Id", false)]
    public void CommandTextReferences_matches_a_prefixed_whole_name(string commandText, string name, bool expected)
    {
        Assert.Equal(expected, DbLoader<BindRecord>.CommandTextReferences(commandText, name));
    }



    private static async Task<List<string>> LoadAndReadBackAsync(bool forceReflection, int batchSize)
    {
        using var connection = await CreateTableAsync();
        var loader = new DbLoader<BindRecord>
        (
            connection,
            InsertSql,
            new DbLoaderOptions { BatchSize = batchSize }
        )
        {
            ForceReflectionParameterBinder = forceReflection,
        };

        await loader.LoadAsync(ToAsyncEnumerable(Records));

        var rows = new List<string>();
        using var select = connection.CreateCommand();
        select.CommandText =
            "SELECT id, typeof(name), name, typeof(shade), shade, typeof(maybe), maybe, typeof(price), price, typeof(flag), flag FROM bind ORDER BY id";
        using var reader = await select.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var cells = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                cells[i] = reader.IsDBNull(i)
                    ? "<null>"
                    : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "<null>";
            }

            rows.Add(string.Join("|", cells));
        }

        return rows;
    }



    private static async Task<SqliteConnection> CreateTableAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var create = connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE bind (id INTEGER PRIMARY KEY, name TEXT, shade INTEGER, maybe INTEGER, price NUMERIC, flag INTEGER)";
        await create.ExecuteNonQueryAsync();
        return connection;
    }



    private static async IAsyncEnumerable<BindRecord> ToAsyncEnumerable(IEnumerable<BindRecord> source)
    {
        foreach (var record in source)
        {
            yield return record;
            await Task.Yield();
        }
    }
}
