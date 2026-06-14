using System.Collections.Concurrent;
using System.Collections.Specialized;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.InMemory;

public partial class MemoryStorage
{
    /// <summary>
    /// Deterministic seed <c>sub</c> for the demo user "alice" — an opaque, stable identifier that is
    /// intentionally NOT derived from the username (ADR-014 §2.2). Exposed so tests can assert
    /// <c>sub</c> ≠ username.
    /// </summary>
    public const string AliceSubjectId = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    /// <summary>Deterministic seed <c>sub</c> for the demo user "bob" (see <see cref="AliceSubjectId"/>).</summary>
    public const string BobSubjectId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

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