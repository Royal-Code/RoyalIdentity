using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Utils;
using System.Linq;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 2 (plan-users-edge-session.md) — characterization of SSO logout notification: on logout the
/// back-channel notifier is invoked for the clients recorded on the session, carrying the current
/// subject and session id. This pins the current behavior (subject = session user name) so the Fase 5
/// migration of <see cref="RoyalIdentity.Users.Defaults.DefaultSignOutManager"/> to <c>SubjectId</c>
/// is a deliberate, visible change rather than a silent regression.
/// </summary>
public class BackChannelLogoutCharacterizationTests : IClassFixture<BackChannelCapturingAppFactory>
{
    private readonly BackChannelCapturingAppFactory factory;

    public BackChannelLogoutCharacterizationTests(BackChannelCapturingAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Logout_NotifiesBackChannelClientsRecordedOnSession()
    {
        var storage = factory.Services.GetRequiredService<MemoryStorage>();
        var (username, password) = CharacterizationSeed.SeedUser(storage, MemoryStorage.DemoRealm);

        var clientId = $"bc-client-{CryptoRandom.CreateUniqueId(6)}";
        storage.GetDemoRealmStore().Clients[clientId] = new Client
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Back-channel Logout Client",
            RequireClientSecret = false,
            RequirePkce = false,
            AllowedGrantTypes = ["authorization_code"],
            AllowedIdentityScopes = { "openid", "profile" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**" },
            BackChannelLogoutUri = { "https://client.example/backchannel-logout" }
        };

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CharacterizationSeed.PostLoginAsync(client, username, password);

        // issuing a code records the client on the session, so logout knows whom to notify
        var code = await client.GetAuthorizeAsync(clientId: clientId, scope: "openid profile");
        Assert.NotNull(code);

        var session = CharacterizationSeed.FindSession(storage, MemoryStorage.DemoRealm, username);
        Assert.NotNull(session);

        await client.LogoutAsync();

        var notified = factory.BackChannelCapture.SingleOrDefault(r => r.ClientId == clientId);
        Assert.NotNull(notified);
        Assert.Equal(username, notified.Subject); // current behavior: subject = session user name
        Assert.Equal(session.Id, notified.SessionId);
    }
}
