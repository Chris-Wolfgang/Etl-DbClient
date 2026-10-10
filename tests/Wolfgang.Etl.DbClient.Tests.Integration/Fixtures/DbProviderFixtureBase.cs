using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Docker.DotNet;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Integration.Fixtures;

/// <summary>
/// Shared plumbing for container-backed fixtures: catches container-start failures
/// during xunit's <see cref="IAsyncLifetime.InitializeAsync"/> so tests can be skipped
/// with a clear reason instead of crashing the whole collection.
/// </summary>
public abstract class DbProviderFixtureBase : IAsyncLifetime, IDbProviderFixture
{
    public abstract string ProviderName { get; }

    public abstract string PagingClauseTemplate { get; }

    public bool Available { get; private set; }

    public string? UnavailableReason { get; private set; }



    /// <summary>
    /// True when this fixture needs Docker to be reachable. SQLite overrides this
    /// to false since it uses an in-memory connection.
    /// </summary>
    protected virtual bool RequiresDocker => true;



    public async Task InitializeAsync()
    {
        // Pre-probe Docker availability. Only "Docker daemon unreachable" should
        // turn into a skip — every other StartAsync failure (bad image tag,
        // schema regression, etc.) must propagate so CI fails loudly.
        UnavailableReason = RequiresDocker
            ? await DockerUnavailableReasonAsync(ProviderName).ConfigureAwait(false)
            : null;

        if (UnavailableReason is null)
        {
            await StartOrCleanUpAsync(StartAsync, StopAsync).ConfigureAwait(false);
            Available = true;
        }
    }



    /// <summary>
    /// Pings the Docker daemon. Returns null when it answers, or a skip reason
    /// naming the probe failure (TLS, permission, daemon-down, socket-path, ...)
    /// so a "skipped" test still gives the reader enough to diagnose it.
    /// </summary>
    /// <remarks>
    /// Excluded from coverage: an infrastructure check whose failure branch only
    /// runs on a machine without Docker, which is never the case on CI runners.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    private static async Task<string?> DockerUnavailableReasonAsync(string providerName)
    {
        try
        {
            using var cfg = new DockerClientConfiguration();
            using var client = cfg.CreateClient();
            await client.System.PingAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return $"{providerName} unavailable: Docker probe failed — {ex.GetType().Name}: {ex.Message}";
        }
    }



    public Task DisposeAsync() =>
        // Always attempt teardown. StopAsync implementations are responsible
        // for tolerating a never-started state (e.g. _container is null).
        StopQuietlyAsync(StopAsync);



    /// <summary>
    /// Runs <paramref name="start"/>. On failure, makes a best-effort
    /// <paramref name="stop"/> and rethrows the start failure, so the test run
    /// surfaces the error instead of silently skipping every test.
    /// </summary>
    internal static async Task StartOrCleanUpAsync(Func<Task> start, Func<Task> stop)
    {
        try
        {
            await start().ConfigureAwait(false);
        }
        catch
        {
            await StopQuietlyAsync(stop).ConfigureAwait(false);
            throw;
        }
    }



    /// <summary>
    /// Runs <paramref name="stop"/>, swallowing any failure: teardown is
    /// best-effort and must never fail a test pass or mask a start failure.
    /// </summary>
    internal static async Task StopQuietlyAsync(Func<Task> stop)
    {
        try
        {
            await stop().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort teardown — a container that refuses to stop cleanly
            // is not a test failure.
        }
    }



    /// <summary>Provision the backing container / database. Throw on failure.</summary>
    protected abstract Task StartAsync();



    /// <summary>Tear down the backing container / database. May throw — caller swallows.</summary>
    protected abstract Task StopAsync();



    public abstract Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);

    public abstract Task ResetSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default);

    public abstract Task SeedAsync(DbConnection connection, int rowCount, CancellationToken cancellationToken = default);



    protected static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
