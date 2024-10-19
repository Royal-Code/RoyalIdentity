using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.InMemory;

public class AuthorizationCodeStore : IAuthorizationCodeStore
{
    private readonly MemoryStorage storage;

    public AuthorizationCodeStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public Task<string> StoreAuthorizationCodeAsync(AuthorizationCode code, CancellationToken ct)
    {
        storage.AuthorizationCodes[code.Code] = code;
        return Task.FromResult(code.Code);
    }

    public Task<AuthorizationCode?> GetAuthorizationCodeAsync(string code, CancellationToken ct)
    {
        storage.AuthorizationCodes.TryGetValue(code, out var authorizationCode);
        return Task.FromResult(authorizationCode);
    }

    public Task RemoveAuthorizationCodeAsync(string code, CancellationToken ct)
    {
        storage.AuthorizationCodes.TryRemove(code, out _);
        return Task.CompletedTask;
    }
}