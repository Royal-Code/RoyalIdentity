using Microsoft.Extensions.Hosting;

namespace RoyalIdentity.Configuration;

/// <summary>
/// Loads the configuration snapshot before the server accepts traffic and refreshes it periodically (plan DF7).
/// The initial load runs inside <see cref="StartAsync"/> via <see cref="IConfigurationSnapshotRefresher.RefreshAsync"/>:
/// if it fails, the host does not start (fail-closed). Each periodic tick runs
/// <see cref="IConfigurationSnapshotRefresher.TryRefreshAsync"/>, which keeps the last-known-good snapshot on
/// failure (plan DF26).
/// </summary>
internal sealed class ConfigurationSnapshotHostedService(
    IConfigurationSnapshotRefresher refresher,
    ConfigurationSnapshotRefreshOptions options,
    TimeProvider clock) : IHostedService, IDisposable
{
    private ShutdownState? shutdownState;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        options.Validate();

        // Initial load is not guarded: a failure here fails StartAsync and the host does not serve traffic.
        await refresher.RefreshAsync(cancellationToken);

        var stoppingSource = new CancellationTokenSource();
        shutdownState = new ShutdownState(
            stoppingSource,
            RunPeriodicAsync(stoppingSource.Token));
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var state = Interlocked.Exchange(ref shutdownState, null);
        if (state is null)
            return;

        try
        {
            await state.StoppingSource.CancelAsync();
            await state.PeriodicLoop.WaitAsync(cancellationToken);
        }
        finally
        {
            state.StoppingSource.Dispose();
        }
    }

    public void Dispose()
    {
        var state = Interlocked.Exchange(ref shutdownState, null);
        if (state is null)
            return;

        state.StoppingSource.Cancel();
        state.StoppingSource.Dispose();
    }

    private async Task RunPeriodicAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(options.RefreshInterval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await refresher.TryRefreshAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
    }

    private sealed record ShutdownState(
        CancellationTokenSource StoppingSource,
        Task PeriodicLoop);
}
