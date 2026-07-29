namespace RoyalIdentity.Configuration;

/// <summary>
/// Asynchronous bootstrap/refresh source for the configuration snapshot (plan DF7). Each backing provides its
/// own implementation and materializes a fresh, storage-independent graph. It never depends on
/// <c>IStorage.ServerOptions</c>, so there is no bootstrap cycle and no hidden synchronous I/O.
/// </summary>
public interface IConfigurationSnapshotSource
{
    Task<ConfigurationSnapshotData> LoadAsync(CancellationToken ct);
}
