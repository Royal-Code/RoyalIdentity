using RoyalIdentity.Models;
using RoyalIdentity.Users.Contexts;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Razor.Services;

public interface ISessionContextService
{
    bool TryGetCurrentRealm([NotNullWhen(true)] out Realm? realm);

    ValueTask<AuthorizationContext?> GetAuthorizationContextAsync(string? returnUrl);
}
