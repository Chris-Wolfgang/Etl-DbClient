// CS8765: the BCL DbConnection/DbCommand setters carry [AllowNull], which an override cannot mirror here.
#pragma warning disable CS8765

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// <c>RowErrorHandling.Skip</c> on a provider that aborts the transaction after a failed statement
/// (PostgreSQL, CockroachDB: SQLSTATE 25P02). SQLite never does that, so a wrapping connection
/// throws a provider-style <see cref="DbException"/> carrying the SQLSTATE instead.
/// </summary>
public class SkipModeAbortedTransactionTests
{
    /// <summary>A <see cref="DbException"/> that reports a SQLSTATE, as Npgsql's PostgresException does.</summary>
    [ExcludeFromCodeCoverage]
    private sealed class SqlStateException : DbException
    {
        public SqlStateException(string sqlState)
            : base("SQLSTATE " + sqlState)
        {
            State = sqlState;
        }



        internal string State { get; }

#if NET5_0_OR_GREATER
        public override string SqlState => State;
#endif
    }



    /// <summary>
    /// A SQLite command that throws the exception <see cref="FailingConnection.FailFor"/> picks for
    /// the row's <c>@Age</c> instead of executing.
    /// </summary>
    [ExcludeFromCodeCoverage]
    private sealed class FailingCommand : DbCommand
    {
        private readonly FailingConnection _owner;
        private readonly SqliteCommand _inner;

        public FailingCommand(FailingConnection owner, SqliteCommand inner)
        {
            _owner = owner;
            _inner = inner;
        }

        public override string CommandText
        {
            get => _inner.CommandText;
            set => _inner.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _inner.CommandTimeout;
            set => _inner.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _inner.CommandType;
            set => _inner.CommandType = value;
        }

        public override bool DesignTimeVisible
        {
            get => _inner.DesignTimeVisible;
            set => _inner.DesignTimeVisible = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _inner.UpdatedRowSource;
            set => _inner.UpdatedRowSource = value;
        }

        protected override DbConnection? DbConnection
        {
            get => _owner;
            set { }
        }

        protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

        protected override DbTransaction? DbTransaction
        {
            get => _inner.Transaction;
            set => _inner.Transaction = (SqliteTransaction?)value;
        }

        public override void Cancel() => _inner.Cancel();

        public override int ExecuteNonQuery()
        {
            ThrowIfFailing();
            return _inner.ExecuteNonQuery();
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            ThrowIfFailing();
            return _inner.ExecuteNonQueryAsync(cancellationToken);
        }

        public override object? ExecuteScalar() => _inner.ExecuteScalar();

        public override void Prepare() => _inner.Prepare();

        protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => _inner.ExecuteReader(behavior);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ThrowIfFailing()
        {
            var age = _inner
                .Parameters
                .Cast<SqliteParameter>()
                .FirstOrDefault(p => string.Equals(p.ParameterName.TrimStart('@'), "Age", StringComparison.Ordinal))
                ?.Value;
            var failure = age is null ? null : _owner.FailFor(Convert.ToInt32(age, System.Globalization.CultureInfo.InvariantCulture));
            if (failure is not null)
            {
                throw failure;
            }
        }
    }



    /// <summary>A SQLite connection whose commands throw the exception <see cref="FailFor"/> returns.</summary>
    [ExcludeFromCodeCoverage]
    private sealed class FailingConnection : DbConnection
    {
        private readonly SqliteConnection _inner = new("Data Source=:memory:");

        internal Func<int, Exception?> FailFor { get; set; } = _ => null;

        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value;
        }

        public override string Database => _inner.Database;
        public override string DataSource => _inner.DataSource;
        public override string ServerVersion => _inner.ServerVersion;
        public override ConnectionState State => _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

        public override void Close() => _inner.Close();

        public override void Open() => _inner.Open();

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => _inner.BeginTransaction(isolationLevel);

        protected override DbCommand CreateDbCommand() => new FailingCommand(this, _inner.CreateCommand());

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }



    private static FailingConnection CreateConnection()
    {
        var conn = new FailingConnection();
        conn.Open();
        using var create = conn.CreateCommand();
        create.CommandText = "CREATE TABLE People (first_name TEXT NOT NULL, last_name TEXT, age INTEGER)";
        create.ExecuteNonQuery();
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



    // Row 1 is the bad row (not-null violation, 23502); every later row is rejected because the
    // transaction is aborted (25P02), as PostgreSQL does.
    private static Exception? PostgresStyleFailures(int age) => age switch
    {
        0 => null,
        1 => new SqlStateException("23502"),
        _ => new SqlStateException("25P02"),
    };



    private static DbLoader<PersonRecord> CreateSkipLoader(FailingConnection conn) => new
    (
        conn,
        "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)",
        new DbLoaderOptions { ErrorHandling = RowErrorHandling.Skip }
    );



#if NET5_0_OR_GREATER
    [Fact]
    public async Task LoadAsync_in_Skip_mode_when_the_transaction_is_aborted_throws_InvalidOperationException()
    {
        using var conn = CreateConnection();
        conn.FailFor = PostgresStyleFailures;
        var sut = CreateSkipLoader(conn);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadAsync(Rows(5)));

        Assert.Contains("BatchCommitSize", ex.Message, StringComparison.Ordinal);
    }



    [Fact]
    public async Task LoadAsync_in_Skip_mode_when_the_transaction_is_aborted_keeps_the_provider_exception_as_inner()
    {
        using var conn = CreateConnection();
        conn.FailFor = PostgresStyleFailures;
        var sut = CreateSkipLoader(conn);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadAsync(Rows(5)));

        Assert.Equal
        (
            "25P02",
            Assert.IsType<SqlStateException>(ex.InnerException).SqlState
        );
    }



    [Fact]
    public async Task LoadAsync_in_Skip_mode_when_the_transaction_is_aborted_reports_only_the_bad_row()
    {
        using var conn = CreateConnection();
        conn.FailFor = PostgresStyleFailures;
        var sut = CreateSkipLoader(conn);
        var failed = new List<int>();
        sut.RowFailed += (_, e) => failed.Add(e.Record.Age);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LoadAsync(Rows(5)));

        Assert.Equal(new[] { 1 }, failed);
    }



    [Fact]
    public async Task LoadAsync_in_Skip_mode_when_a_row_fails_with_another_SqlState_still_skips_it()
    {
        using var conn = CreateConnection();
        conn.FailFor = age => age == 1 ? new SqlStateException("23502") : null;
        var sut = CreateSkipLoader(conn);
        var rowFailures = 0;
        sut.RowFailed += (_, _) => rowFailures++;

        await sut.LoadAsync(Rows(3));

        Assert.Equal(1, rowFailures);
        Assert.Equal(2, sut.CurrentItemCount);
    }
#else
    [Fact]
    public async Task LoadAsync_in_Skip_mode_before_net5_cannot_read_the_SqlState_and_skips_every_failed_row()
    {
        using var conn = CreateConnection();
        conn.FailFor = PostgresStyleFailures;
        var sut = CreateSkipLoader(conn);
        var rowFailures = 0;
        sut.RowFailed += (_, _) => rowFailures++;

        await sut.LoadAsync(Rows(5));

        Assert.Equal(4, rowFailures);
    }
#endif
}
