using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Xunit;

// The Parameters property is the obsolete DynamicParameters setter; it is the simplest way to
// observe the callback Dapper's reader path does not raise.
#pragma warning disable CS0618

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class ExtractorParameterCallbackTests
{
    [Fact]
    public async Task ExtractAsync_raises_OnCompleted_on_the_command_parameters_after_the_rows_are_read()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var spy = new CallbackSpy();
        spy.Add("@age", 20);

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age > @age",
            parameters: new Dictionary<string, object>(),
            options: null
        )
        {
            Parameters = spy,
        };

        var rows = new List<PersonRecord>();
        await foreach (var row in sut.ExtractAsync())
        {
            Assert.Equal(0, spy.OnCompletedCount);
            rows.Add(row);
        }

        Assert.Equal(3, rows.Count);
        Assert.Equal(1, spy.OnCompletedCount);
    }



    [Fact]
    public async Task ExtractAsync_raises_OnCompleted_when_the_consumer_stops_early()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var spy = new CallbackSpy();
        spy.Add("@age", 20);

        var sut = new DbExtractor<PersonRecord>
        (
            conn,
            "SELECT first_name, last_name, age FROM People WHERE age > @age",
            parameters: new Dictionary<string, object>(),
            options: null
        )
        {
            Parameters = spy,
        };

        // Read one row, then dispose the enumerator: leaving early is the case under test.
        var enumerator = sut.ExtractAsync().GetAsyncEnumerator();
        await enumerator.MoveNextAsync();
        await enumerator.DisposeAsync();

        Assert.Equal(1, spy.OnCompletedCount);
    }



    private sealed class CallbackSpy : DynamicParameters, SqlMapper.IParameterCallbacks
    {
        public int OnCompletedCount { get; private set; }

        void SqlMapper.IParameterCallbacks.OnCompleted() => OnCompletedCount++;
    }
}
