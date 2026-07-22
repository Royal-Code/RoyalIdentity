namespace RoyalIdentity.Configuration;

/// <summary>
/// Reloads the configuration snapshot from its source and publishes it (plan DF7). Used by the hosted
/// refresher for the initial and periodic loads, and by legacy configuration writes (realm create/update/
/// enable/disable) so a change made at runtime becomes visible to the synchronous consumers immediately,
/// instead of waiting for the next periodic refresh. It publishes/invalidates only when the full load succeeds.
/// </summary>
public interface IConfigurationSnapshotRefresher
{
	/// <summary>
	/// Reloads and publishes the snapshot; throws when the load fails, leaving the last published snapshot
	/// untouched. Used by the bootstrap (fail-closed) and by legacy writes that must observe their own change.
	/// </summary>
	Task RefreshAsync(CancellationToken ct = default);

	/// <summary>
	/// Reloads and publishes the snapshot; except for caller-requested cancellation, it does not throw: on
	/// failure it keeps the last-known-good snapshot
	/// indefinitely, records the failure (observable via <see cref="IConfigurationSnapshot.LastRefreshFailureUtc"/>)
	/// and logs it without any sensitive payload (plan DF26). Returns whether a new snapshot was published.
	/// Used by the periodic refresher.
	/// </summary>
	Task<bool> TryRefreshAsync(CancellationToken ct = default);
}
