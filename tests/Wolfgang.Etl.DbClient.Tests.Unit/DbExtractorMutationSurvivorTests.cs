using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Pins <see cref="DbExtractor{TRecord}"/> behaviour that the Stryker run on DbExtractor.cs showed
/// no test observing (#474): the command timeout reaching the command, and <c>CountAsync</c>
/// disposing a connection the extractor created from a <see cref="DbProviderFactory"/> while only
/// closing a caller's connection it was asked to manage.
/// </summary>
public class DbExtractorMutationSurvivorTests
{
    private const string TwoRowsSql = "SELECT 1 AS Id UNION ALL SELECT 2";



    [ExcludeFromCodeCoverage]
    public sealed record IdRow
    {
        public long Id { get; init; }
    }



    [Fact]
    public async Task ExtractAsync_when_CommandTimeout_is_set_passes_it_to_the_command()
    {
        using var connection = new CapturingSqliteConnection();
        await connection.OpenAsync();

        var extractor = new DbExtractor<IdRow>
        (
            connection,
            TwoRowsSql,
            new DbExtractorOptions { CommandTimeout = TimeSpan.FromSeconds(9) }
        );

        var rows = new List<IdRow>();
        await foreach (var row in extractor.ExtractAsync())
        {
            rows.Add(row);
        }

        Assert.Equal(2, rows.Count);
        Assert.NotEmpty(connection.Commands);
        Assert.All(connection.Commands, command => Assert.Equal(9, command.CommandTimeout));
    }



    [Fact]
    public async Task CountAsync_when_the_extractor_created_the_connection_disposes_it()
    {
        var factory = new TrackingSqliteFactory();
        var extractor = new DbExtractor<IdRow>(factory, "Data Source=:memory:", TwoRowsSql, new DbExtractorOptions());

        var count = await extractor.CountAsync();

        Assert.Equal(2, count);
        var created = Assert.Single(factory.Connections);
        Assert.True(created.WasDisposed, "the factory-created connection was not disposed");
    }



    [Fact]
    public async Task CountAsync_when_managing_a_caller_connection_closes_it_without_disposing_it()
    {
        using var connection = new TrackedSqliteConnection();
        var extractor = new DbExtractor<IdRow>
        (
            connection,
            TwoRowsSql,
            new DbExtractorOptions { ManageConnection = true }
        );

        var count = await extractor.CountAsync();

        Assert.Equal(2, count);
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.False(connection.WasDisposed, "the caller's connection was disposed");
    }



    /// <summary>An in-memory SQLite connection that records every command created on it.</summary>
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



    /// <summary>An in-memory SQLite connection that records whether it was disposed.</summary>
    [ExcludeFromCodeCoverage]
    private sealed class TrackedSqliteConnection : SqliteConnection
    {
        internal TrackedSqliteConnection()
            : base("Data Source=:memory:")
        {
        }



        internal bool WasDisposed { get; private set; }



        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }



    /// <summary>A provider factory that hands out <see cref="TrackedSqliteConnection"/>s.</summary>
    [ExcludeFromCodeCoverage]
    private sealed class TrackingSqliteFactory : DbProviderFactory
    {
        internal List<TrackedSqliteConnection> Connections { get; } = [];



        public override DbConnection CreateConnection()
        {
            var connection = new TrackedSqliteConnection();
            Connections.Add(connection);
            return connection;
        }
    }
}
