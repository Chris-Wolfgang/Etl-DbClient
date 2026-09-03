using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Pins the pre-cancelled-token contract: a loader handed an already-cancelled token must read
/// <em>nothing</em> from its source.
/// </summary>
/// <remarks>
/// The guard exists in <c>LoadWorkerAsync</c>, but nothing in this repository proved it — the
/// TestKit loader contract base that covers this is not adopted here, only the dry-run one. These
/// tests count actual reads rather than trusting the guard's presence.
/// <para>
/// It matters more here than for most loaders: a database cursor is exactly the non-replayable
/// source where a silently-drained first row cannot be recovered.
/// </para>
/// </remarks>
public class PreCancelledTokenTests
{
    /// <summary>A source that records how many items were actually pulled from it.</summary>
    private sealed class CountingSource
    {
        internal int ItemsRead;

        // The token is deliberately NOT observed here. If this source honoured cancellation it
        // would throw on its own and the test would pass without proving anything about the
        // loader. Ignoring it is what makes "zero items read" attributable to the loader's
        // upfront guard rather than to the source's good behaviour.
#pragma warning disable RCS1163 // Unused parameter - intentional, see above.
        internal async IAsyncEnumerable<PersonRecord> ItemsAsync
        (
            [EnumeratorCancellation] CancellationToken token = default
        )
#pragma warning restore RCS1163
        {
            foreach (var name in new[] { "Alice", "Bob", "Carol" })
            {
                ItemsRead++;
                yield return new PersonRecord { FirstName = name, LastName = "X", Age = 30 };
                await Task.Yield();
            }
        }
    }


    private static SqliteConnection Connection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE People (first_name TEXT, last_name TEXT, age INTEGER);";
        cmd.ExecuteNonQuery();
        return conn;
    }


    [Fact]
    public async Task LoadAsync_with_a_pre_cancelled_token_reads_nothing()
    {
        using var conn = Connection();
        var source = new CountingSource();

        var sut = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)",
            options: null
        );

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>
        (
            () => sut.LoadAsync(source.ItemsAsync(cts.Token), cts.Token)
        );

        Assert.Equal(0, source.ItemsRead);
    }


    [Fact]
    public async Task LoadAsync_with_a_pre_cancelled_token_writes_nothing()
    {
        using var conn = Connection();
        var source = new CountingSource();

        var sut = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)",
            options: null
        );

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>
        (
            () => sut.LoadAsync(source.ItemsAsync(cts.Token), cts.Token)
        );

        using var count = conn.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM People";
        Assert.Equal(0L, (long)count.ExecuteScalar()!);
    }


    [Fact]
    public async Task LoadAsync_with_a_live_token_still_reads_everything()
    {
        // Guards against "fixing" cancellation by never reading at all.
        using var conn = Connection();
        var source = new CountingSource();

        var sut = new DbLoader<PersonRecord>
        (
            conn,
            "INSERT INTO People (first_name, last_name, age) VALUES (@FirstName, @LastName, @Age)",
            options: null
        );

        await sut.LoadAsync(source.ItemsAsync(), CancellationToken.None);

        Assert.Equal(3, source.ItemsRead);
    }
}
