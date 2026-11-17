using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Owned-connection (DbProviderFactory) constructors dispose their connection when an operation
/// ends. SQLite happily reopens a disposed connection, which is why the rest of the suite cannot see
/// the problem; these tests use a connection that behaves like SqlClient / Npgsql and refuses to
/// open once disposed.
/// </summary>
public class OwnedConnectionLifecycleTests
{
    [ExcludeFromCodeCoverage]
    private sealed class StrictConnection : DbConnection
    {
        private readonly SqliteConnection _inner = new("Data Source=:memory:");
        private bool _disposed;

        internal bool FailOpen { get; init; }

        internal bool IsDisposed => _disposed;

        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value;
        }

        public override string Database => _inner.Database;
        public override string DataSource => _inner.DataSource;
        public override string ServerVersion => _inner.ServerVersion;
        public override System.Data.ConnectionState State => _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

        public override void Close() => _inner.Close();

        public override void Open()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(StrictConnection));
            }

            if (FailOpen)
            {
                throw new InvalidOperationException("simulated open failure");
            }

            _inner.Open();
        }

        protected override DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => _inner.BeginTransaction(isolationLevel);

        protected override DbCommand CreateDbCommand()
        {
            var command = _inner.CreateCommand();
            command.Connection = _inner;
            return command;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposed = true;
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }



    [ExcludeFromCodeCoverage]
    private sealed class StrictFactory : DbProviderFactory
    {
        internal List<StrictConnection> Created { get; } = new();

        internal bool FailOpen { get; init; }

        public override DbConnection CreateConnection()
        {
            var connection = new StrictConnection { FailOpen = FailOpen };
            Created.Add(connection);
            return connection;
        }
    }



    private const string SelectNothing = "SELECT 1 AS id, 'x' AS first_name, 'y' AS last_name, 30 AS age WHERE 0=1";



    [Fact]
    public async Task CountAsync_then_ExtractAsync_on_an_owned_connection_both_succeed()
    {
        var factory = new StrictFactory();
        var sut = new DbExtractor<PersonRecord>(factory, "Data Source=:memory:", SelectNothing, options: null);

        var count = await sut.CountAsync();
        var rows = await sut.ExtractAsync().ToListAsync();

        Assert.Equal(0, count);
        Assert.Empty(rows);
        Assert.Equal(2, factory.Created.Count);
        Assert.All(factory.Created, c => Assert.True(c.IsDisposed));
    }



    [Fact]
    public async Task ExtractAsync_twice_on_an_owned_connection_uses_a_fresh_connection_each_time()
    {
        var factory = new StrictFactory();
        var sut = new DbExtractor<PersonRecord>(factory, "Data Source=:memory:", SelectNothing, options: null);

        await sut.ExtractAsync().ToListAsync();
        await sut.ExtractAsync().ToListAsync();

        Assert.Equal(2, factory.Created.Count);
        Assert.All(factory.Created, c => Assert.True(c.IsDisposed));
    }



    [Fact]
    public async Task ExtractAsync_when_the_owned_connection_fails_to_open_still_disposes_it()
    {
        var factory = new StrictFactory { FailOpen = true };
        var sut = new DbExtractor<PersonRecord>(factory, "Data Source=:memory:", SelectNothing, options: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExtractAsync().ToListAsync().AsTask());

        Assert.True(Assert.Single(factory.Created).IsDisposed);
    }



    [Fact]
    public async Task CountAsync_when_the_owned_connection_fails_to_open_still_disposes_it()
    {
        var factory = new StrictFactory { FailOpen = true };
        var sut = new DbExtractor<PersonRecord>(factory, "Data Source=:memory:", SelectNothing, options: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CountAsync());

        Assert.True(Assert.Single(factory.Created).IsDisposed);
    }



    [Fact]
    public async Task LoadAsync_twice_on_an_owned_connection_uses_a_fresh_connection_each_time()
    {
        var factory = new StrictFactory();
        var sut = new DbLoader<PersonRecord>(factory, "Data Source=:memory:", "SELECT 1", options: null);

        await sut.LoadAsync(Array.Empty<PersonRecord>().ToAsyncEnumerable());
        await sut.LoadAsync(Array.Empty<PersonRecord>().ToAsyncEnumerable());

        Assert.Equal(2, factory.Created.Count);
        Assert.All(factory.Created, c => Assert.True(c.IsDisposed));
    }



    [Fact]
    public async Task LoadAsync_when_the_owned_connection_fails_to_open_still_disposes_it()
    {
        var factory = new StrictFactory { FailOpen = true };
        var sut = new DbLoader<PersonRecord>(factory, "Data Source=:memory:", "SELECT 1", options: null);

        await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => sut.LoadAsync(Array.Empty<PersonRecord>().ToAsyncEnumerable())
        );

        Assert.True(Assert.Single(factory.Created).IsDisposed);
    }
}
