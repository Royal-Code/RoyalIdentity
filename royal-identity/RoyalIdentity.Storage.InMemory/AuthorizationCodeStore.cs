using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Tokens;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class AuthorizationCodeStore : IAuthorizationCodeStore
{
    private readonly ConcurrentDictionary<string, AuthorizationCode> authorizationCodes;

    public AuthorizationCodeStore(ConcurrentDictionary<string, AuthorizationCode> authorizationCodes)
    {
        this.authorizationCodes = authorizationCodes;
    }

    public Task<string> StoreAuthorizationCodeAsync(AuthorizationCode code, CancellationToken ct)
    {
        authorizationCodes[code.Code] = code;
        return Task.FromResult(code.Code);
    }

    public Task<AuthorizationCode?> GetAuthorizationCodeAsync(string code, CancellationToken ct)
    {
        authorizationCodes.TryGetValue(code, out var authorizationCode);
        return Task.FromResult(authorizationCode);
    }

    public Task RemoveAuthorizationCodeAsync(string code, CancellationToken ct)
    {
        authorizationCodes.TryRemove(code, out _);
        return Task.CompletedTask;
    }
}