// CS8765: the BCL DbConnection setters carry [AllowNull], which an override cannot mirror here.
#pragma warning disable CS8765

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class SkipModeConnectionFailureTests
{
    /// <summary>A SQLite connection that can be told to report itself as no longer open.</summary>
    [ExcludeFromCodeCoverage]
    private sealed class DroppableConnection : DbConnection
    {
        private readonly SqliteConnection _inner = new("Data Source=:memory:");

        internal bool Dropped { get; set; }

        internal SqliteConnection Inner => _inner;

        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value;
        }

        public override string Database => _inner.Database;
        public override string DataSource => _inner.DataSource;
        public override string ServerVersion => _inner.ServerVersion;
        public override ConnectionState State => Dropped ? ConnectionState.Closed : _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

        public override void Close() => _inner.Close();

        public override void Open() => _inner.Open();

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => _inner.BeginTransaction(isolationLevel);

        protected override DbCommand CreateDbCommand() => _inner.CreateCommand();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }



    private static DroppableConnection CreateConnection()
    {
        var conn = new DroppableConnection();
        conn.Open();
        using var create = conn.CreateCommand();
        create.CommandText = "CREATE TABLE People (first_name TEXT NOT NULL, last_name TEXT, age INTEGER)";
        create.ExecuteNonQuery();
        conn.Inner.CreateFunction<string>
        (
            "drop_connection",
            () =>
            {
                conn.Dropped = true;
                throw new InvalidOperationException("connection dropped");
            }
        );
        return conn;
    }



    private static IAsyncEnumerable<PersonRecord> Rows(int count)
    {
        var rows = new List<PersonRecord>();
        for (var i = 0; i < count; i++)
        {
            rows.Add(new PersonRecord { FirstName = "n" + i, LastName = "l", Age = i });
        }

        return rows.ToAsyncEnumerable();
    }



    [Fact]
    public async Task LoadAsync_in_Skip_mode_when_the_connection_is_lost_aborts_instead_of_skipping_every_row()
    {
        using var conn = CreateConnection();
        var sut = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (drop_connection(), @LastName, @Age)",
            new DbLoaderOptions { ErrorHandling = RowErrorHandling.Skip }
        );
        var rowFailures = 0;
        sut.RowFailed += (_, _) => rowFailures++;

        await Assert.ThrowsAnyAsync<Exception>(() => sut.LoadAsync(Rows(5)));

        Assert.Equal(0, rowFailures);
    }



    [Fact]
    public async Task LoadAsync_in_Skip_mode_when_only_a_row_fails_still_skips_it()
    {
        using var conn = CreateConnection();
        var sut = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (CASE WHEN @Age = 1 THEN NULL ELSE @FirstName END, @LastName, @Age)",
            new DbLoaderOptions { ErrorHandling = RowErrorHandling.Skip }
        );
        var rowFailures = 0;
        sut.RowFailed += (_, _) => rowFailures++;

        await sut.LoadAsync(Rows(3));

        Assert.Equal(1, rowFailures);
        Assert.Equal(2, sut.CurrentItemCount);
    }
}
