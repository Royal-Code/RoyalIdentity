using RoyalIdentity.Options;
using Tests.Storage.Configuration.Support;

namespace Tests.Storage.Configuration;

/// <summary>
/// The hosted refresher (plan Fase 3, DF7): it validates the mandatory interval and loads the initial snapshot
/// inside <c>StartAsync</c>, and a failure there fails startup (fail-closed) so the host never serves traffic
/// with unknown configuration.
/// </summary>
public class ConfigurationSnapshotHostedServiceTests
{
	[Fact]
	public async Task StartAsync_LoadsInitialSnapshot_BeforeReturning()
	{
		using var harness = new SnapshotTestHarness();
		harness.Source.Data = SnapshotTestHarness.BuildData(new ServerOptions { IssuerUri = "https://issuer.test" }, "server");
		var service = harness.HostedService;

		await service.StartAsync(CancellationToken.None);

		Assert.True(harness.Snapshot.IsLoaded);
		Assert.Equal(1, harness.Source.LoadCount);
		Assert.Equal("https://issuer.test", harness.Snapshot.ServerOptions.IssuerUri);

		await service.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task StartAsync_WhenInitialLoadFails_ThrowsAndDoesNotStart()
	{
		using var harness = new SnapshotTestHarness();
		harness.Source.Failure = new InvalidOperationException("no server_options row");
		var service = harness.HostedService;

		await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));

		Assert.False(harness.Snapshot.IsLoaded);
	}

	[Fact]
	public async Task StartAsync_WithNonPositiveRefreshInterval_ThrowsBeforeLoading()
	{
		using var harness = new SnapshotTestHarness(refreshInterval: TimeSpan.Zero);
		harness.Source.Data = SnapshotTestHarness.BuildData(new ServerOptions(), "server");
		var service = harness.HostedService;

		await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));

		Assert.Equal(0, harness.Source.LoadCount);
		Assert.False(harness.Snapshot.IsLoaded);
	}

	[Fact]
	public async Task StopAsync_CancelsAndAwaitsPeriodicRefresh()
	{
		using var harness = new SnapshotTestHarness(refreshInterval: TimeSpan.FromMilliseconds(20));
		harness.Source.Data = SnapshotTestHarness.BuildData(new ServerOptions(), "server");
		var service = harness.HostedService;
		await service.StartAsync(CancellationToken.None);

		var periodicEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		harness.Source.Loader = async ct =>
		{
			periodicEntered.TrySetResult();
			try
			{
				await Task.Delay(Timeout.InfiniteTimeSpan, ct);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				cancellationObserved.TrySetResult();
				throw;
			}

			return harness.Source.Data!;
		};

		await periodicEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
		using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		await service.StopAsync(stopTimeout.Token);

		Assert.True(cancellationObserved.Task.IsCompletedSuccessfully);
	}
}
