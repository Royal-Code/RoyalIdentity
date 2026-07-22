using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Configuration;

/// <summary>
/// The singleton <see cref="IConfigurationSnapshot"/>. It holds the current <see cref="PublishedConfigurationSnapshot"/>
/// behind an atomic reference swap, so readers always see a fully-published graph and never a half-updated one.
/// The hosted refresher is the only writer: it calls <see cref="Publish"/> on success and
/// <see cref="MarkRefreshFailure"/> on a failed periodic refresh, keeping the last-known-good indefinitely
/// (plan DF26).
/// </summary>
internal sealed class ConfigurationSnapshotHolder(TimeProvider clock) : IConfigurationSnapshot
{
	private volatile PublishedConfigurationSnapshot? current;
	private long lastRefreshFailureTicks;

	public bool IsLoaded => current is not null;

	public ServerOptions ServerOptions => Require().CopyServerOptions();

	public Realm? FindRealmByPath(string path)
	{
		ArgumentNullException.ThrowIfNull(path);
		return Require().FindRealmByPath(path);
	}

	public IReadOnlyCollection<string> RealmPaths => current?.RealmPaths ?? [];

	public DateTimeOffset LoadedAtUtc => Require().LoadedAtUtc;

	public DateTimeOffset? LastRefreshFailureUtc
	{
		get
		{
			var ticks = Interlocked.Read(ref lastRefreshFailureTicks);
			return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
		}
	}

	/// <summary>
	/// Publishes a new snapshot atomically and returns the realm paths of the previous snapshot, so the caller
	/// can invalidate exactly the named options that were affected (previous ∪ new). A successful publish clears
	/// the last-recorded failure.
	/// </summary>
	public IReadOnlyCollection<string> Publish(ConfigurationSnapshotData data)
	{
		ArgumentNullException.ThrowIfNull(data);

		var previousPaths = current?.RealmPaths.ToArray() ?? [];
		current = new PublishedConfigurationSnapshot(data, clock.GetUtcNow());
		Interlocked.Exchange(ref lastRefreshFailureTicks, 0);

		return previousPaths;
	}

	/// <summary>Records that a periodic refresh failed; the last-known-good snapshot is preserved (plan DF26).</summary>
	public void MarkRefreshFailure()
		=> Interlocked.Exchange(ref lastRefreshFailureTicks, clock.GetUtcNow().UtcTicks);

	private PublishedConfigurationSnapshot Require()
		=> current ?? throw new InvalidOperationException(
			"The configuration snapshot has not been loaded yet. It is published by the hosted refresher before the server accepts traffic.");
}
