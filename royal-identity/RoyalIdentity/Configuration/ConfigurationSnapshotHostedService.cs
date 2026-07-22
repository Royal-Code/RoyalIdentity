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
	private CancellationTokenSource? stoppingSource;
	private Task? periodicLoop;

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		options.Validate();

		// Initial load is not guarded: a failure here fails StartAsync and the host does not serve traffic.
		await refresher.RefreshAsync(cancellationToken);

		stoppingSource = new CancellationTokenSource();
		periodicLoop = RunPeriodicAsync(stoppingSource.Token);
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (stoppingSource is null || periodicLoop is null)
			return;

		await stoppingSource.CancelAsync();
		await periodicLoop.WaitAsync(cancellationToken);
	}

	public void Dispose()
	{
		stoppingSource?.Cancel();
		stoppingSource?.Dispose();
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
}
