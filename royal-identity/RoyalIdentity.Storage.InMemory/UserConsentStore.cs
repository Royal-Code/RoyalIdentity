using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;

namespace RoyalIdentity.Storage.InMemory;

public class UserConsentStore : IUserConsentStore
{
    private readonly MemoryStorage storage;

    public UserConsentStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public Task StoreUserConsentAsync(Consent consent, CancellationToken ct)
    {
        var key = consent.SubjectId + "." + consent.ClientId;
        storage.Consents.AddOrUpdate(key, _ => consent, (_,_) => consent);
        return Task.CompletedTask;
    }

    public Task<Consent?> GetUserConsentAsync(string subjectId, string clientId, CancellationToken ct)
    {
        var key = subjectId + "." + clientId;
        storage.Consents.TryGetValue(key, out var consent);
        return Task.FromResult(consent);
    }

    public Task RemoveUserConsentAsync(string subjectId, string clientId, CancellationToken ct)
    {
        var key = subjectId + "." + clientId;
        storage.Consents.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}