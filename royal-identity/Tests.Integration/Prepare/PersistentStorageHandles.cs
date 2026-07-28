using RoyalIdentity.Options;
using Tests.UserAccounts;

namespace Tests.Integration.Prepare;

/// <summary>Provider-neutral identity of one realm in the current fixture.</summary>
public sealed record TestRealmHandle(string Id, string Path);

/// <summary>Provider-neutral identity of one seeded client.</summary>
public sealed record TestClientHandle(string RealmId, string ClientId);

/// <summary>Provider-neutral identity and credentials of one seeded test subject.</summary>
public sealed record TestSubjectHandle(string SubjectId, string Username, string Password);

/// <summary>
/// Immutable handles owned by one persistent factory. They intentionally contain no materialized
/// <c>Realm</c>, mutable storage object or static fixture state.
/// </summary>
public sealed class PersistentStorageHandles
{
    public PersistentStorageHandles()
    {
        Server = new(
            Constants.Server.Realms.ServerRealm,
            Constants.Server.Realms.ServerRealm);
        Account = new(
            Constants.Server.Realms.AccountRealm,
            Constants.Server.Realms.AccountRealm);
        Admin = new(
            Constants.Server.Realms.AdminRealm,
            Constants.Server.Realms.AdminRealm);
        Demo = new("demo_realm", "demo");

        ServerAdminClient = new(Server.Id, "server_admin");
        DemoClient = new(Demo.Id, "demo_client");
        DemoConsentClient = new(Demo.Id, "demo_consent_client");

        Alice = new(
            UserAccountsModuleSeed.AliceSubjectId,
            UserAccountsModuleSeed.AliceUsername,
            UserAccountsModuleSeed.AlicePassword);
        Bob = new(
            UserAccountsModuleSeed.BobSubjectId,
            UserAccountsModuleSeed.BobUsername,
            UserAccountsModuleSeed.BobPassword);
    }

    public TestRealmHandle Server { get; }

    public TestRealmHandle Account { get; }

    public TestRealmHandle Admin { get; }

    public TestRealmHandle Demo { get; }

    public TestClientHandle ServerAdminClient { get; }

    public TestClientHandle DemoClient { get; }

    public TestClientHandle DemoConsentClient { get; }

    public TestSubjectHandle Alice { get; }

    public TestSubjectHandle Bob { get; }

    public IReadOnlyList<TestRealmHandle> Realms => [Server, Account, Admin, Demo];
}
