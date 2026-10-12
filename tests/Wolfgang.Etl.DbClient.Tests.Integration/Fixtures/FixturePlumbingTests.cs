using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;

/// <summary>
/// The start/stop plumbing every container fixture relies on. Its failure paths
/// only fire when a container misbehaves, so they are pinned here directly
/// rather than waiting for a broken image to exercise them.
/// </summary>
public sealed class FixturePlumbingTests
{
    [Fact]
    public async Task StartOrCleanUpAsync_when_start_and_stop_both_throw_stops_then_rethrows_the_start_failure()
    {
        var stopped = false;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>
        (
            () => DbProviderFixtureBase.StartOrCleanUpAsync
            (
                () => throw new InvalidOperationException("start failed"),
                () =>
                {
                    stopped = true;
                    throw new TimeoutException("stop failed");
                }
            )
        );

        Assert.Equal("start failed", ex.Message);
        Assert.True(stopped);
    }
}
