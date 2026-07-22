using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Options;

namespace RoyalIdentity.Configuration;

/// <summary>
/// Loads the configuration snapshot on a fresh scope and publishes it atomically, then invalidates exactly the
/// cookie named-options affected by the change: the default scheme plus the union of the previous and new realm
/// schemes (plan DF7). Foreign cookie schemes are never touched. A failed load in <see cref="RefreshAsync"/>
/// throws and leaves the last published snapshot untouched — nothing is published or invalidated unless the
/// full load succeeds. <see cref="TryRefreshAsync"/> adds the last-known-good policy for the periodic path.
/// </summary>
internal sealed class ConfigurationSnapshotRefresher(
	IServiceScopeFactory scopeFactory,
	ConfigurationSnapshotHolder holder,
	IOptionsMonitorCache<CookieAuthenticationOptions> cookieOptionsCache,
	ILogger<ConfigurationSnapshotRefresher> logger,
	TimeProvider clock) : IConfigurationSnapshotRefresher
{
	private readonly SemaphoreSlim refreshGate = new(1, 1);

	public async Task RefreshAsync(CancellationToken ct = default)
	{
		await refreshGate.WaitAsync(ct);
		try
		{
			await using var scope = scopeFactory.CreateAsyncScope();
			var source = scope.ServiceProvider.GetRequiredService<IConfigurationSnapshotSource>();

			var data = await source.LoadAsync(ct);

			var previousPaths = holder.Publish(data);
			InvalidateCookieOptions(previousPaths, holder.RealmPaths);
		}
		finally
		{
			refreshGate.Release();
		}
	}

	public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
	{
		try
		{
			await RefreshAsync(ct);
			return true;
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			holder.MarkRefreshFailure();
			if (holder.IsLoaded)
			{
				var age = clock.GetUtcNow() - holder.LoadedAtUtc;
				logger.LogError(ex,
					"Configuration snapshot refresh failed; keeping the last-known-good snapshot (age {AgeSeconds:F0}s).",
					age.TotalSeconds);
			}
			else
			{
				logger.LogError(ex,
					"Configuration snapshot refresh failed before any snapshot had been published.");
			}
			return false;
		}
	}

	private void InvalidateCookieOptions(
		IReadOnlyCollection<string> previousPaths, IReadOnlyCollection<string> currentPaths)
	{
		cookieOptionsCache.TryRemove(Server.DefaultCookieAuthenticationScheme);

		foreach (var path in previousPaths.Union(currentPaths, StringComparer.Ordinal))
			cookieOptionsCache.TryRemove($"{Server.RealmAuthenticationNamePrefix}{path}");
	}
}
