using RoyalIdentity.Models.Keys;

namespace RoyalIdentity.Contracts.Storage;

public interface IKeyStore
{
    public Task<KeyParameters> GetKeyAsync(string keyId, CancellationToken ct);

    public Task<IReadOnlyList<KeyParameters>> GetKeysAsync(CancellationToken ct);

    public Task<IReadOnlyList<KeyParameters>> GetValidationKeysAsync(CancellationToken ct);
}