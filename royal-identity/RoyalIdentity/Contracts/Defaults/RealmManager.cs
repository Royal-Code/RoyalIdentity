using Microsoft.Extensions.Logging;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults;

public class RealmManager : IRealmManager
{
    private readonly IStorage storage;
    private readonly IConfigurationSnapshot snapshot;
    private readonly IConfigurationSnapshotRefresher snapshotRefresher;
    private readonly ILogger logger;

    public RealmManager(
        IStorage storage,
        IConfigurationSnapshot snapshot,
        IConfigurationSnapshotRefresher snapshotRefresher,
        ILogger<RealmManager> logger)
    {
        this.storage = storage;
        this.snapshot = snapshot;
        this.snapshotRefresher = snapshotRefresher;
        this.logger = logger;
    }

    public async ValueTask<Realm> CreateAsync(
        string path,
        string domain,
        string displayName,
        CancellationToken ct = default)
    {
        var realmStore = storage.Realms;

        var existing = await realmStore.GetByPathAsync(path, ct);
        if (existing is not null)
            throw new InvalidOperationException($"A realm with path '{path}' already exists.");

        var existingByDomain = await realmStore.GetByDomainAsync(domain, ct);
        if (existingByDomain is not null)
            throw new InvalidOperationException($"A realm with domain '{domain}' already exists.");

        var realm = new Realm(null, domain, path, displayName, false, new RealmOptions(snapshot.ServerOptions));

        await realmStore.SaveAsync(realm, ct);

        // Legacy write (plan DF28): the new realm must be visible to the synchronous consumers immediately,
        // so request a snapshot reload instead of waiting for the periodic refresh (plan DF7).
        await snapshotRefresher.RefreshAsync(ct);

        logger.LogInformation("Realm created: {RealmId} ({RealmPath})", realm.Id, realm.Path);

        // Phase 5: dispatch realm-created event with realm scope
        return realm;
    }

    public async ValueTask UpdateAsync(Realm realm, CancellationToken ct = default)
    {
        var existing = await GetNonInternalAsync(realm.Id, ct);

        if (realm.Path != existing.Path)
            throw new InvalidOperationException(
                $"Realm path cannot be changed via UpdateAsync. Current: '{existing.Path}', Attempted: '{realm.Path}'.");

        if (realm.Domain != existing.Domain)
            throw new InvalidOperationException(
                $"Realm domain cannot be changed via UpdateAsync. Current: '{existing.Domain}', Attempted: '{realm.Domain}'.");

        await storage.Realms.SaveAsync(realm, ct);
        await snapshotRefresher.RefreshAsync(ct);
        logger.LogInformation("Realm updated: {RealmId}", realm.Id);
    }

    public async ValueTask EnableAsync(string realmId, CancellationToken ct = default)
    {
        var realm = await GetNonInternalAsync(realmId, ct);
        realm.Enabled = true;
        await storage.Realms.SaveAsync(realm, ct);
        await snapshotRefresher.RefreshAsync(ct);
        logger.LogInformation("Realm enabled: {RealmId}", realmId);
    }

    public async ValueTask DisableAsync(string realmId, CancellationToken ct = default)
    {
        var realm = await GetNonInternalAsync(realmId, ct);
        realm.Enabled = false;
        await storage.Realms.SaveAsync(realm, ct);
        await snapshotRefresher.RefreshAsync(ct);
        logger.LogInformation("Realm disabled: {RealmId}", realmId);
    }

    private async ValueTask<Realm> GetNonInternalAsync(string realmId, CancellationToken ct)
    {
        var realm = await storage.Realms.GetByIdAsync(realmId, ct)
            ?? throw new InvalidOperationException($"Realm '{realmId}' not found.");

        if (realm.Internal)
            throw new InvalidOperationException($"Realm '{realmId}' is internal and cannot be modified.");

        return realm;
    }
}
