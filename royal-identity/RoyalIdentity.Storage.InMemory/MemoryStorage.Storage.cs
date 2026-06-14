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

    public MemoryStorage(IPasswordProtector passwordProtector, TimeProvider clock)
    {
        realmMemoryStore.AddRange(
            Realms.Values.Select(
                r => new KeyValuePair<string, RealmMemoryStore>(
                    r.Id,
                    new RealmMemoryStore(r, r.Id == "server"))));

        this.passwordProtector = passwordProtector;
        this.clock = clock;
    }

    public RealmMemoryStore GetRealmMemoryStore(Realm realm)
    {
        if (realmMemoryStore.TryGetValue(realm.Id, out var store))
            return store;
        throw RealmNotFound(realm);
    }

    public RealmMemoryStore GetServerRealmStore() => GetRealmMemoryStore(ServerRealm);

    public RealmMemoryStore GetDemoRealmStore() => GetRealmMemoryStore(DemoRealm);

    IRealmStore IStorage.Realms => new RealmStore(Realms, realmMemoryStore);

    IAuthorizeParametersStore IStorage.AuthorizeParameters => new AuthorizeParametersStore(AuthorizeParameters);

    public IAccessTokenStore GetAccessTokenStore(Realm realm)
        => new AccessTokenStore(GetRealmMemoryStore(realm).AccessTokens);

    public IRefreshTokenStore GetRefreshTokenStore(Realm realm)
        => new RefreshTokenStore(GetRealmMemoryStore(realm).RefreshTokens);

    public IAuthorizationCodeStore GetAuthorizationCodeStore(Realm realm)
        => new AuthorizationCodeStore(GetRealmMemoryStore(realm).AuthorizationCodes);

    public IUserConsentStore GetUserConsentStore(Realm realm)
        => new UserConsentStore(GetRealmMemoryStore(realm).UserConsents);

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
            return new ResourceStore(
                store.ResourceServers,
                store.IdentityScopes);

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
            return new UserSessionStore(store.UserSessions, clock);

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
