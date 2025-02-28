using System.Collections.Concurrent;
using System.Collections.Specialized;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.InMemory;

public partial class MemoryStorage
{
    private static readonly ServerOptions serverOptions = new();

    private static readonly Realm serverRealm = new(
        "server",
        "royalidentity.server",
        "server",
        "RoyalIdentity Server", 
        true, 
        new RealmOptions(serverOptions));

    private static readonly Realm accountRealm = new(
        "account",
        "royalidentity.account",
        "account",
        "RoyalIdentity Account Realm",
        true,
        new RealmOptions(serverOptions));

    private static readonly Realm adminRealm = new(
        "admin",
        "royalidentity.admin",
        "admin",
        "RoyalIdentity Admin Realm",
        true,
        new RealmOptions(serverOptions));

    private static readonly Realm demoRealm = new(
        "demo_realm",
        "demo.com",
        "demo",
        "Demo Realm",
        false,
        new RealmOptions(serverOptions));

    public ServerOptions ServerOptions => serverOptions;

    public ConcurrentDictionary<string, Realm> Realms { get; } = new()
    {
        ["server"] = serverRealm,
        ["account"] = accountRealm,
        ["admin"] = adminRealm,
        ["demo_realm"] = demoRealm
    };

    [Obsolete("Use Realms instead")]
    public ConcurrentDictionary<string, Client> Clients { get; } = new()
    {
        ["server_admin"] = new Client
        {
            Realm = serverRealm,
            Id = "server_admin",
            Name = "Administrative server portal",
            RequireClientSecret = false,
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5200/**", "https://localhost:7200/**" }
        },
        ["demo_client"] = new Client
        {
            Realm = demoRealm,
            Id = "demo_client",
            Name = "Demo Client",
            RequireClientSecret = false,
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost/callback", "http://localhost:5000/**", "https://localhost:5001/**" }
        },
        ["demo_consent_client"] = new Client
        {
            Realm = demoRealm,
            Id = "demo_consent_client",
            Name = "Demo Consent Client",
            RequireClientSecret = false,
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "email", "api", "api:read", "api:write"  },
            AllowedResponseTypes = { "code" },
            RequireConsent = true,
            RedirectUris = { "http://localhost/callback", "http://localhost:5000/**", "https://localhost:5001/**" }
        }
    };

    public ConcurrentDictionary<string, AccessToken> AccessTokens { get; } = new();

    public ConcurrentDictionary<string, AuthorizationCode> AuthorizationCodes { get; } = new();

    public ConcurrentDictionary<string, NameValueCollection> AuthorizeParameters { get; } = new();

    public ConcurrentDictionary<string, Consent> Consents { get; } = new();

    public ConcurrentDictionary<string, RefreshToken> RefreshTokens { get; } = new();
}