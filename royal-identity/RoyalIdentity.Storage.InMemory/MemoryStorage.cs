using System.Collections.Concurrent;
using System.Collections.Specialized;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.InMemory;

public partial class MemoryStorage
{
    private static readonly ServerOptions serverOptions = new();

    public static Realm ServerRealm { get; } = new(
        Constants.Server.Realms.ServerRealm,
        Constants.Server.Realms.ServerDomain,
        Constants.Server.Realms.ServerRealm,
        Constants.Server.Realms.ServerDisplayName, 
        true, 
        new RealmOptions(serverOptions));

    public static Realm AccountRealm { get; } = new(
        Constants.Server.Realms.AccountRealm,
        Constants.Server.Realms.AccountDomain,
        Constants.Server.Realms.AccountRealm,
        Constants.Server.Realms.AccountDisplayName,
        true,
        new RealmOptions(serverOptions));

    public static Realm AdminRealm { get; } = new(
        Constants.Server.Realms.AdminRealm,
        Constants.Server.Realms.AdminDomain,
        Constants.Server.Realms.AdminRealm,
        Constants.Server.Realms.AdminDisplayName,
        true,
        new RealmOptions(serverOptions));

    public static Realm DemoRealm { get; } = new(
        "demo_realm",
        "demo.com",
        "demo",
        "Demo Realm",
        false,
        new RealmOptions(serverOptions));

    static MemoryStorage()
    {
        DemoRealm.Options.Branding.PrimaryColor = "#6366F1";
    }

    public ServerOptions ServerOptions => serverOptions;

    public ConcurrentDictionary<string, Realm> Realms { get; } = new()
    {
        ["server"] = ServerRealm,
        ["account"] = AccountRealm,
        ["admin"] = AdminRealm,
        ["demo_realm"] = DemoRealm
    };

    public ConcurrentDictionary<string, NameValueCollection> AuthorizeParameters { get; } = new();
}