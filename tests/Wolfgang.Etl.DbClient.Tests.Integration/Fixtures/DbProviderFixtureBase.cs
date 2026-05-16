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
[ExcludeFromCodeCoverage]
public abstract class DbProviderFixtureBase : IAsyncLifetime, IDbProviderFixture
{
    public abstract string ProviderName { get; }

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
        if (RequiresDocker && !await IsDockerReachableAsync().ConfigureAwait(false))
        {
            Available = false;
            UnavailableReason = $"{ProviderName} unavailable: Docker daemon is not reachable.";
            return;
        }

        try
        {
            await StartAsync().ConfigureAwait(false);
            Available = true;
        }
        catch
        {
            // Real failure: best-effort cleanup, then rethrow so the test run
            // surfaces the error instead of silently skipping every test.
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // Swallow secondary failures during emergency cleanup.
            }

            throw;
        }
    }



    private static async Task<bool> IsDockerReachableAsync()
    {
        try
        {
            using var cfg = new DockerClientConfiguration();
            using var client = cfg.CreateClient();
            await client.System.PingAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }



    public async Task DisposeAsync()
    {
        // Always attempt teardown. StopAsync implementations are responsible
        // for tolerating a never-started state (e.g. _container is null).
        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort teardown — never fail a test pass because a container
            // refused to stop cleanly.
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
