using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class UserConsentStore : IUserConsentStore
{
    private readonly ConcurrentDictionary<string, Consent> consents;

    public UserConsentStore(ConcurrentDictionary<string, Consent> consents)
    {
        this.consents = consents;
    }

    public Task StoreUserConsentAsync(Consent consent, CancellationToken ct)
    {
        var key = consent.SubjectId + "." + consent.ClientId;
        consents.AddOrUpdate(key, _ => consent, (_,_) => consent);
        return Task.CompletedTask;
    }

    public Task<Consent?> GetUserConsentAsync(string subjectId, string clientId, CancellationToken ct)
    {
        var key = subjectId + "." + clientId;
        consents.TryGetValue(key, out var consent);
        return Task.FromResult(consent);
    }

    public Task RemoveUserConsentAsync(string subjectId, string clientId, CancellationToken ct)
    {
        var key = subjectId + "." + clientId;
        consents.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}