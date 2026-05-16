using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
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



    public async Task InitializeAsync()
    {
        try
        {
            await StartAsync().ConfigureAwait(false);
            Available = true;
        }
        catch (Exception ex)
        {
            Available = false;
            UnavailableReason = $"{ProviderName} unavailable: {ex.GetType().Name}: {ex.Message}";
        }
    }



    public async Task DisposeAsync()
    {
        if (!Available)
        {
            return;
        }

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
