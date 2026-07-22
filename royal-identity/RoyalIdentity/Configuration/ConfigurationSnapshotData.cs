using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Configuration;

/// <summary>
/// The complete, storage-independent configuration graph produced by an
/// <see cref="IConfigurationSnapshotSource"/> in one shot (plan DF7). The realms and server options are
/// already materialized copies detached from any backing store; the snapshot publishes them atomically.
/// </summary>
public sealed class ConfigurationSnapshotData
{
	public required ServerOptions ServerOptions { get; init; }

	public required IReadOnlyList<Realm> Realms { get; init; }
}
