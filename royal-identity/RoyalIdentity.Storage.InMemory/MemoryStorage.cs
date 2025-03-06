using System.Collections.Concurrent;
using System.Collections.Specialized;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.InMemory;

public partial class MemoryStorage
{
    private static readonly ServerOptions serverOptions = new();

    public static Realm ServerRealm { get; } = new(
        "server",
        "royalidentity.server",
        "server",
        "RoyalIdentity Server", 
        true, 
        new RealmOptions(serverOptions));

    public static Realm AccountRealm { get; } = new(
        "account",
        "royalidentity.account",
        "account",
        "RoyalIdentity Account Realm",
        true,
        new RealmOptions(serverOptions));

    public static Realm AdminRealm { get; } = new(
        "admin",
        "royalidentity.admin",
        "admin",
        "RoyalIdentity Admin Realm",
        true,
        new RealmOptions(serverOptions));

    public static Realm DemoRealm { get; } = new(
        "demo_realm",
        "demo.com",
        "demo",
        "Demo Realm",
        false,
        new RealmOptions(serverOptions));

    public ServerOptions ServerOptions => serverOptions;

    public ConcurrentDictionary<string, Realm> Realms { get; } = new()
    {
        ["server"] = ServerRealm,
        ["account"] = AccountRealm,
        ["admin"] = AdminRealm,
        ["demo_realm"] = DemoRealm
    };

    public ConcurrentDictionary<string, AccessToken> AccessTokens { get; } = new();

    public ConcurrentDictionary<string, AuthorizationCode> AuthorizationCodes { get; } = new();

    public ConcurrentDictionary<string, NameValueCollection> AuthorizeParameters { get; } = new();

    public ConcurrentDictionary<string, Consent> Consents { get; } = new();

    public ConcurrentDictionary<string, RefreshToken> RefreshTokens { get; } = new();
}