using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Users.Contracts;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace RoyalIdentity.Storage.InMemory;

public partial class MemoryStorage : IStorage
{
    private readonly ConcurrentDictionary<string, RealmMemoryStore> realmMemoryStore = new();

    private readonly IPasswordProtector passwordProtector;
    private readonly TimeProvider clock;
    private readonly IHttpContextAccessor accessor;

    public MemoryStorage(IPasswordProtector passwordProtector, TimeProvider clock, IHttpContextAccessor accessor)
    {
        realmMemoryStore.AddRange(
            Realms.Values.Select(
                r => new KeyValuePair<string, RealmMemoryStore>(
                    r.Id,
                    new RealmMemoryStore(r, r.Id == "server"))));

        this.passwordProtector = passwordProtector;
        this.clock = clock;
        this.accessor = accessor;
    }

    public RealmMemoryStore GetRealmMemoryStore(Realm realm)
    {
        if (realmMemoryStore.TryGetValue(realm.Id, out var store))
            return store;
        throw RealmNotFound(realm);
    }

    public RealmMemoryStore GetServerRealmStore() => GetRealmMemoryStore(ServerRealm);

    public RealmMemoryStore GetDemoRealmStore() => GetRealmMemoryStore(DemoRealm);

    IRealmStore IStorage.Realms => new RealmStore(Realms);

    IUserConsentStore IStorage.UserConsents => new UserConsentStore(Consents);

    IAccessTokenStore IStorage.AccessTokens => new AccessTokenStore(AccessTokens);

    IRefreshTokenStore IStorage.RefreshTokens => new RefreshTokenStore(RefreshTokens);

    IAuthorizationCodeStore IStorage.AuthorizationCodes => new AuthorizationCodeStore(AuthorizationCodes);

    IAuthorizeParametersStore IStorage.AuthorizeParameters => new AuthorizeParametersStore(AuthorizeParameters);

    public IClientStore GetClientStore(Realm realm)
    {
        if (realmMemoryStore.TryGetValue(realm.Id, out var store))
            return new ClientStore(store.Clients);

        throw RealmNotFound(realm);
    }

    public IKeyStore GetKeyStore(Realm realm)
    {
        if (realmMemoryStore.TryGetValue(realm.Id, out var store))
            return new KeyStore(store.KeyParameters);

        throw RealmNotFound(realm);
    }

    public IResourceStore GetResourceStore(Realm realm)
    {
        if (realmMemoryStore.TryGetValue(realm.Id, out var store))
            return new ResourceStore(store.IdentityResources, store.ApiScopes, store.ApiResources);

        throw RealmNotFound(realm);
    }


    public IUserDetailsStore GetUserDetailsStore(Realm realm)
    {
        if (realmMemoryStore.TryGetValue(realm.Id, out var store))
            return new UserStore(store.UsersDetails, realm.Options.Account, GetUserSessionStore(realm), passwordProtector, clock);

        throw RealmNotFound(realm);
    }

    public IUserSessionStore GetUserSessionStore(Realm realm)
    {
        if (realmMemoryStore.TryGetValue(realm.Id, out var store))
            return new UserSessionStore(store.UserSessions, clock, accessor);

        throw RealmNotFound(realm);
    }

    public IUserStore GetUserStore(Realm realm)
    {
        if (realmMemoryStore.TryGetValue(realm.Id, out var store))
            return new UserStore(store.UsersDetails, realm.Options.Account, GetUserSessionStore(realm), passwordProtector, clock);

        throw RealmNotFound(realm);
    }

    [DebuggerStepThrough]
    private Exception RealmNotFound(Realm realm)
    {
        return new ArgumentException($"The realm with the Id ‘{realm.Id}’ was not found", nameof(realm));
    }
}
