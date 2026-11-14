using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Pins <see cref="DbLoader{TRecord}"/> behaviour that the Stryker run on DbLoader.cs showed no
/// test observing (#474): the command timeout reaching the command, the loader leaving an
/// unmanaged closed connection alone, <c>BatchSize &gt; 1</c> keeping one transaction even
/// when <c>BatchCommitSize</c> is set, cancellation noticed between items, and the multi-row
/// INSERT template scanner's handling of literals and parameter-name prefixes.
/// </summary>
public class DbLoaderMutationSurvivorTests
{
    [ExcludeFromCodeCoverage]
    public sealed record Row
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }



    [ExcludeFromCodeCoverage]
    public sealed record PrefixRow
    {
        public long A { get; init; }

        public long A1 { get; init; }
    }



    private const string InsertRowSql = "INSERT INTO row (id, name) VALUES (@Id, @Name)";



    [Fact]
    public async Task LoadAsync_when_CommandTimeout_is_set_passes_it_to_the_command()
    {
        using var connection = new CapturingSqliteConnection();
        await connection.OpenAsync();
        await CreateRowTableAsync(connection);
        connection.Commands.Clear();

        var loader = new DbLoader<Row>
        (
            connection,
            InsertRowSql,
            new DbLoaderOptions { CommandTimeout = TimeSpan.FromSeconds(7) }
        );

        await loader.LoadAsync(ToAsyncEnumerable(Rows(2)));

        Assert.NotEmpty(connection.Commands);
        Assert.All(connection.Commands, command => Assert.Equal(7, command.CommandTimeout));
    }



    [Fact]
    public async Task LoadAsync_when_connection_is_closed_and_not_managed_does_not_open_it()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var loader = new DbLoader<Row>(connection, InsertRowSql, new DbLoaderOptions());

        await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => loader.LoadAsync(ToAsyncEnumerable(Rows(1)))
        );

        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
    }



    [Fact]
    public async Task LoadAsync_when_BatchSize_is_above_1_ignores_BatchCommitSize_and_rolls_back_everything_on_failure()
    {
        using var connection = TestDb.CreateConnection();
        await CreateRowTableAsync(connection);

        var loader = new DbLoader<Row>
        (
            connection,
            InsertRowSql,
            new DbLoaderOptions { BatchSize = 3, BatchCommitSize = 2 }
        );

        // The fifth row repeats the first row's primary key, so the second batch fails.
        var rows = Rows(4).Concat([new Row { Id = 1, Name = "duplicate" }]);

        await Assert.ThrowsAsync<SqliteException>(() => loader.LoadAsync(ToAsyncEnumerable(rows)));

        Assert.Equal(0L, await CountRowsAsync(connection));
    }



    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    public async Task LoadAsync_when_token_is_cancelled_between_items_stops_before_the_next_item(int batchSize, int insertBatchSize)
    {
        using var connection = TestDb.CreateConnection();
        using var cts = new CancellationTokenSource();
        var loader = new DbLoader<Row>
        (
            connection,
            InsertRowSql,
            new DbLoaderOptions { IsDryRun = true, BatchSize = batchSize, InsertBatchSize = insertBatchSize }
        );

        // The source ignores the token and cancels it after handing out the second row, so
        // only the loader's own check between items can stop the load.
        await Assert.ThrowsAnyAsync<OperationCanceledException>
        (
            () => loader.LoadAsync(CancelAfterSecond(Rows(5), cts), cts.Token)
        );

        Assert.True(loader.CurrentItemCount < 5, $"CurrentItemCount was {loader.CurrentItemCount}");
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_template_has_a_word_literal_binds_only_the_parameters()
    {
        using var connection = TestDb.CreateConnection();
        await ExecuteAsync(connection, "CREATE TABLE note (id INTEGER PRIMARY KEY, kind TEXT, name TEXT)");

        var loader = new DbLoader<Row>
        (
            connection,
            "INSERT INTO note (id, kind, name) VALUES (@Id, 'literal', @Name)",
            new DbLoaderOptions { InsertBatchSize = 2 }
        );

        await loader.LoadAsync(ToAsyncEnumerable(Rows(3)));

        Assert.Equal
        (
            ["1|literal|Name1", "2|literal|Name2", "3|literal|Name3"],
            await ReadAsync(connection, "SELECT id, kind, name FROM note ORDER BY id")
        );
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_template_has_a_lone_at_sign_in_a_literal_ignores_it()
    {
        using var connection = TestDb.CreateConnection();
        await ExecuteAsync(connection, "CREATE TABLE note (id INTEGER PRIMARY KEY, kind TEXT, name TEXT)");

        var loader = new DbLoader<Row>
        (
            connection,
            "INSERT INTO note (id, kind, name) VALUES (@Id, '@', @Name)",
            new DbLoaderOptions { InsertBatchSize = 2 }
        );

        await loader.LoadAsync(ToAsyncEnumerable(Rows(2)));

        Assert.Equal
        (
            ["1|@|Name1", "2|@|Name2"],
            await ReadAsync(connection, "SELECT id, kind, name FROM note ORDER BY id")
        );
    }



    [Fact]
    public async Task LoadAsync_with_InsertBatchSize_when_one_parameter_name_prefixes_another_keeps_row_keys_distinct()
    {
        // Row-numbered keys are "@name_row". Without the separator, column A of row 11
        // ("@A11") and column A1 of row 1 ("@A11") would collide in one chunk of 12 rows.
        using var connection = TestDb.CreateConnection();
        await ExecuteAsync(connection, "CREATE TABLE pair (a INTEGER, a1 INTEGER)");

        var source = Enumerable
            .Range(0, 12)
            .Select(i => new PrefixRow { A = i, A1 = 100 + i })
            .ToArray();

        var loader = new DbLoader<PrefixRow>
        (
            connection,
            "INSERT INTO pair (a, a1) VALUES (@A, @A1)",
            new DbLoaderOptions { InsertBatchSize = 12 }
        );

        await loader.LoadAsync(ToAsyncEnumerable(source));

        Assert.Equal
        (
            source.Select(r => r.A.ToString(CultureInfo.InvariantCulture) + "|" + r.A1.ToString(CultureInfo.InvariantCulture)).ToList(),
            await ReadAsync(connection, "SELECT a, a1 FROM pair ORDER BY a")
        );
    }



    private static IEnumerable<Row> Rows(int count) => Enumerable
        .Range(1, count)
        .Select(i => new Row { Id = i, Name = "Name" + i.ToString(CultureInfo.InvariantCulture) });



    private static async Task CreateRowTableAsync(DbConnection connection)
        => await ExecuteAsync(connection, "CREATE TABLE row (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");



    private static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }



    private static async Task<long> CountRowsAsync(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM row";
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }



    private static async Task<List<string>> ReadAsync(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            var cells = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                cells[i] = Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
            }

            rows.Add(string.Join("|", cells));
        }

        return rows;
    }



    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }



    private static async IAsyncEnumerable<Row> CancelAfterSecond(IEnumerable<Row> source, CancellationTokenSource cts)
    {
        var handedOut = 0;
        foreach (var item in source)
        {
            yield return item;
            handedOut++;
            if (handedOut == 2)
            {
#if NET8_0_OR_GREATER
                await cts.CancelAsync();
#else
                cts.Cancel();
#endif
            }
            await Task.Yield();
        }
    }



    /// <summary>An in-memory SQLite connection that records every command Dapper creates on it.</summary>
    [ExcludeFromCodeCoverage]
    private sealed class CapturingSqliteConnection : SqliteConnection
    {
        internal CapturingSqliteConnection()
            : base("Data Source=:memory:")
        {
        }



        internal List<DbCommand> Commands { get; } = [];



        protected override DbCommand CreateDbCommand()
        {
            var command = base.CreateDbCommand();
            Commands.Add(command);
            return command;
        }
    }
}
