using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts;

public interface IRealmManager
{
    /// <summary>Creates a new realm with the given path, domain and display name.</summary>
    ValueTask<Realm> CreateAsync(
        string path,
        string domain,
        string displayName,
        CancellationToken ct = default);

    /// <summary>Updates configuration of an existing realm.</summary>
    ValueTask UpdateAsync(Realm realm, CancellationToken ct = default);

    /// <summary>Enables a disabled realm.</summary>
    ValueTask EnableAsync(string realmId, CancellationToken ct = default);

    /// <summary>Disables a realm. Internal realms cannot be disabled.</summary>
    ValueTask DisableAsync(string realmId, CancellationToken ct = default);
}
