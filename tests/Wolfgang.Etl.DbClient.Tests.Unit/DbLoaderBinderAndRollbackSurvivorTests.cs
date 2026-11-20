using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Pins <see cref="DbLoader{TRecord}"/> behaviour that the scheduled Stryker run on main a41665e
/// (#474) reported no test observing: the reflection binder's property filter, the parameter-name
/// scan when the name starts the command text, and the Error log when a rollback itself fails.
/// </summary>
public class DbLoaderBinderAndRollbackSurvivorTests
{
    private const string InsertSql = "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)";



    [ExcludeFromCodeCoverage]
    public sealed class IndexedRecord
    {
        private readonly Dictionary<int, string> _cells = new();



        public string Name { get; set; } = string.Empty;



        public string this[int index] => _cells.TryGetValue(index, out var cell) ? cell : string.Empty;



        // A setter with no getter: the binder has nothing to read, so it must leave it out.
#pragma warning disable S2376, CA1044 // Write-only on purpose: the case under test.
        public string WriteOnly
        {
            set => _cells[0] = value;
        }
#pragma warning restore S2376, CA1044
    }



    [Fact]
    public void CreateReflectionParameterBinder_for_StoredProcedure_skips_indexers_and_write_only_properties()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        var loader = new DbLoader<IndexedRecord>
        (
            connection,
            "usp_insert_indexed",
            new DbLoaderOptions { CommandType = CommandType.StoredProcedure }
        );

        Assert.Equal
        (
            ["Name"],
            loader.CreateReflectionParameterBinder().Names
        );
    }



    [Theory]
    [InlineData("Id = @Id", "Id", true)]
    [InlineData("Id", "Id", false)]
    public void CommandTextReferences_when_the_name_starts_the_text_checks_the_next_match(string commandText, string name, bool expected)
    {
        Assert.Equal(expected, DbLoader<IndexedRecord>.CommandTextReferences(commandText, name));
    }



    [Fact]
    public async Task LoadAsync_when_the_rollback_fails_logs_Error_and_rethrows_the_original_exception()
    {
        var logger = new SpyLogger<DbLoader<ContractRecord>>();
        using var connection = TestDb.CreateContractLoaderConnection();
        var loader = new DbLoader<ContractRecord>(connection, InsertSql, new DbLoaderOptions(), logger: logger);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>
        (
            async () => await loader.LoadAsync(CloseConnectionThenFail(connection))
        );

        Assert.Equal("source failed", thrown.Message);
        var error = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("Failed to rollback transaction", error.Message, StringComparison.Ordinal);
        Assert.NotNull(error.Exception);
    }



    // Hands out one row, then closes the connection under the open auto-transaction (which ends
    // it, so the loader's rollback throws) and fails.
    private static IAsyncEnumerable<ContractRecord> CloseConnectionThenFail(SqliteConnection connection) =>
        Enumerable
            .Range(1, 2)
            .Select(i => i == 1 ? new ContractRecord { Name = "first", Value = 1 } : CloseAndThrow(connection))
            .ToAsyncEnumerable();



    private static ContractRecord CloseAndThrow(SqliteConnection connection)
    {
        connection.Close();
        throw new InvalidOperationException("source failed");
    }
}
