using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class ElapsedTimeFreezeTests
{
    [Fact]
    public async Task Extractor_report_ElapsedMilliseconds_does_not_advance_after_the_extract_completes()
    {
        using var conn = await TestDb.CreateConnectionWithDataAsync(3);
        var sut = new DbExtractor<PersonRecord>(conn, "SELECT first_name, last_name, age FROM People", new System.Collections.Generic.Dictionary<string, object>(), options: null);

        await sut.ExtractAsync().ToListAsync();
        var first = sut.GetProgressReport().ElapsedMilliseconds;
        await Task.Delay(150);
        var second = sut.GetProgressReport().ElapsedMilliseconds;

        Assert.Equal(first, second);
    }



    [Fact]
    public async Task Loader_report_ElapsedMilliseconds_does_not_advance_after_the_load_completes()
    {
        using var conn = TestDb.CreateContractLoaderConnection();
        var sut = new DbLoader<ContractRecord>(conn, "INSERT INTO ContractItems (Name, Value) VALUES (@Name, @Value)", options: null);
        var items = new[] { new ContractRecord { Name = "a", Value = 1 } };

        await sut.LoadAsync(items.ToAsyncEnumerable());
        var first = sut.GetProgressReport().ElapsedMilliseconds;
        await Task.Delay(150);
        var second = sut.GetProgressReport().ElapsedMilliseconds;

        Assert.Equal(first, second);
    }
}
