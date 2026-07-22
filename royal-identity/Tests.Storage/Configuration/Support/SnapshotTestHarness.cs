using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RoyalIdentity.Configuration;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using Tests.Storage.Support;

namespace Tests.Storage.Configuration.Support;

/// <summary>
/// Controllable snapshot source: returns configurable data or fails on demand, and counts loads. Registered as
/// a singleton in the harness (the real EF source is scoped); the refresher still resolves it on a fresh scope.
/// </summary>
internal sealed class FakeConfigurationSnapshotSource : IConfigurationSnapshotSource
{
	private int loadCount;

	public ConfigurationSnapshotData? Data { get; set; }

	public Exception? Failure { get; set; }

	public Func<CancellationToken, Task<ConfigurationSnapshotData>>? Loader { get; set; }

	public int LoadCount => Volatile.Read(ref loadCount);

	public Task<ConfigurationSnapshotData> LoadAsync(CancellationToken ct)
	{
		Interlocked.Increment(ref loadCount);
		ct.ThrowIfCancellationRequested();

		if (Loader is not null)
			return Loader(ct);

		if (Failure is not null)
			return Task.FromException<ConfigurationSnapshotData>(Failure);

		return Task.FromResult(Data ?? throw new InvalidOperationException("No snapshot data configured."));
	}
}

/// <summary>
/// <see cref="IOptionsMonitorCache{TOptions}"/> that records every <see cref="TryRemove"/> name, so tests can
/// assert which cookie schemes were invalidated after a refresh (default + affected realm schemes only).
/// </summary>
internal sealed class SpyCookieOptionsCache : IOptionsMonitorCache<CookieAuthenticationOptions>
{
	private readonly OptionsCache<CookieAuthenticationOptions> inner = new();

	public List<string> RemovedNames { get; } = [];

	public void Clear() => inner.Clear();

	public CookieAuthenticationOptions GetOrAdd(string? name, Func<CookieAuthenticationOptions> createOptions)
		=> inner.GetOrAdd(name, createOptions);

	public bool TryAdd(string? name, CookieAuthenticationOptions options) => inner.TryAdd(name, options);

	public bool TryRemove(string? name)
	{
		if (name is not null)
			RemovedNames.Add(name);

		return inner.TryRemove(name);
	}
}

/// <summary>
/// Composes the real (internal) snapshot holder/refresher/hosted-service through the public
/// <see cref="ConfigurationSnapshotServiceCollectionExtensions.AddConfigurationSnapshot"/> seam, wired to a
/// controllable source, a spy cookie cache and a controllable clock — so plan-Fase-3 behaviors can be driven
/// and asserted without a running host.
/// </summary>
internal sealed class SnapshotTestHarness : IDisposable
{
	public static readonly DateTimeOffset Start = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

	private readonly ServiceProvider provider;

	public SnapshotTestHarness(TimeSpan? refreshInterval = null)
	{
		Source = new FakeConfigurationSnapshotSource();
		CookieCache = new SpyCookieOptionsCache();
		Clock = new FakeClock(Start);

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<TimeProvider>(Clock);
		services.AddSingleton<IConfigurationSnapshotSource>(Source);
		services.AddSingleton(new ConfigurationSnapshotRefreshOptions
		{
			RefreshInterval = refreshInterval ?? TimeSpan.FromMinutes(5),
		});
		services.AddSingleton<IOptionsMonitorCache<CookieAuthenticationOptions>>(CookieCache);
		services.AddConfigurationSnapshot();

		provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
	}

	public FakeConfigurationSnapshotSource Source { get; }

	public SpyCookieOptionsCache CookieCache { get; }

	public FakeClock Clock { get; }

	public IConfigurationSnapshot Snapshot => provider.GetRequiredService<IConfigurationSnapshot>();

	public IConfigurationSnapshotRefresher Refresher => provider.GetRequiredService<IConfigurationSnapshotRefresher>();

	public IHostedService HostedService => provider.GetServices<IHostedService>().Single();

	/// <summary>Builds a data graph with the given server options and realm paths bound to it.</summary>
	public static ConfigurationSnapshotData BuildData(ServerOptions serverOptions, params string[] realmPaths)
	{
		var realms = realmPaths
			.Select(path => new Realm(path, $"{path}.test", path, $"Realm {path}", false, new RealmOptions(serverOptions)))
			.ToList();

		return new ConfigurationSnapshotData { ServerOptions = serverOptions, Realms = realms };
	}

	public void Dispose() => provider.Dispose();
}
