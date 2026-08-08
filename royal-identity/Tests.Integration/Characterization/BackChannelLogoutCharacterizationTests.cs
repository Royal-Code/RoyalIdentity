using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Utils;
using System.Linq;
using System.Text.Json;
using System.Web;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 2 (plan-users-edge-session.md) — characterization of SSO logout notification: on logout the
/// front/back-channel clients recorded on the session are used to build logout callbacks. This pins
/// the current behavior (subject = session user name in back-channel) so the Fase 5 migration of
/// <see cref="RoyalIdentity.Users.Defaults.DefaultSignOutManager"/> to <c>SubjectId</c> is a
/// deliberate, visible change rather than a silent regression.
/// </summary>
public class BackChannelLogoutCharacterizationTests : IClassFixture<BackChannelCapturingAppFactory>
{
    private readonly BackChannelCapturingAppFactory factory;

    public BackChannelLogoutCharacterizationTests(BackChannelCapturingAppFactory factory)
    {
        this.factory = factory;
    }

    private static string GetQueryValue(string url, string key)
    {
        var uri = new Uri(url, UriKind.RelativeOrAbsolute);
        if (!uri.IsAbsoluteUri)
            uri = new Uri(new Uri("http://localhost"), uri);

        var values = HttpUtility.ParseQueryString(uri.Query).GetValues(key);
        Assert.NotNull(values);
        Assert.Single(values);
        return values[0]!;
    }

    [Fact]
    public async Task Logout_NotifiesBackChannelClientsRecordedOnSession()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);

        var clientId = $"bc-client-{CryptoRandom.CreateUniqueId(6)}";
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, registered =>
        {
            registered.Name = "Back-channel Logout Client";
            registered.RequireClientSecret = false;
            registered.RequirePkce = false;
            registered.AllowedGrantTypes.Add("authorization_code");
            registered.AllowedIdentityScopes.UnionWith(["openid", "profile"]);
            registered.AllowedResponseTypes.Add("code");
            registered.RedirectUris.Add("https://localhost:5000/callback");
            registered.BackChannelLogoutUris.Add("https://client.example/backchannel-logout");
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CharacterizationSeed.PostLoginAsync(
            client, subject.Username, subject.Password, factory.Handles.Demo.Path);

        // issuing a code records the client on the session, so logout knows whom to notify
        var code = await client.GetAuthorizeAsync(clientId: clientId, scope: "openid profile");
        Assert.NotNull(code);

        var session = await CharacterizationSeed.FindSessionAsync(
            factory, factory.Handles.Demo, subject);
        Assert.NotNull(session);

        await client.LogoutAsync();

        var notified = factory.BackChannelCapture.SingleOrDefault(r => r.ClientId == clientId);
        Assert.NotNull(notified);
        Assert.Equal(session.SubjectId, notified.Subject); // Fase 5: back-channel subject is the stable SubjectId (was username)
        Assert.Equal(session.Id, notified.SessionId);
    }

    [Fact]
    public async Task Logout_WritesFrontChannelCallbackForClientsRecordedOnSession()
    {
        var subject = await CharacterizationSeed.SeedUserAsync(factory, factory.Handles.Demo);

        var clientId = $"fc-client-{CryptoRandom.CreateUniqueId(6)}";
        const string frontChannelUri = "https://client.example/frontchannel-logout";
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, registered =>
        {
            registered.Name = "Front-channel Logout Client";
            registered.RequireClientSecret = false;
            registered.RequirePkce = false;
            registered.AllowedGrantTypes.Add("authorization_code");
            registered.AllowedIdentityScopes.UnionWith(["openid", "profile"]);
            registered.AllowedResponseTypes.Add("code");
            registered.RedirectUris.Add("https://localhost:5000/callback");
            registered.FrontChannelLogoutUris.Add(frontChannelUri);
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CharacterizationSeed.PostLoginAsync(
            client, subject.Username, subject.Password, factory.Handles.Demo.Path);

        var code = await client.GetAuthorizeAsync(clientId: clientId, scope: "openid profile");
        Assert.NotNull(code);

        var session = await CharacterizationSeed.FindSessionAsync(
            factory, factory.Handles.Demo, subject);
        Assert.NotNull(session);

        var logoutResponse = await client.LogoutAsync();
        await using var body = await logoutResponse.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(body);
        var redirect = json.RootElement.GetProperty("redirect").GetString();
        Assert.NotNull(redirect);

        var logoutId = GetQueryValue(redirect, "logoutId");
        using var scope = factory.Services.CreateScope();
        var messageStore = scope.ServiceProvider.GetRequiredService<IMessageStore>();
        var callbackMessage = await messageStore.ReadAsync<LogoutCallbackMessage>(logoutId, default);
        Assert.NotNull(callbackMessage);

        var payload = callbackMessage.Data;
        Assert.NotNull(payload);
        Assert.Equal(session.Id, payload.SessionId);
        Assert.Equal(
            Oidc.Routes.BuildEndSessionCallbackUrl(factory.Handles.Demo.Path),
            payload.SignOutIframeUrl);

        var frontChannelLogout = Assert.Single(payload.FrontChannelLogout!);
        var frontChannelLogoutUri = new Uri(frontChannelLogout);
        Assert.Equal(frontChannelUri, frontChannelLogoutUri.GetLeftPart(UriPartial.Path));

        var query = HttpUtility.ParseQueryString(frontChannelLogoutUri.Query);
        Assert.False(string.IsNullOrWhiteSpace(query[JwtRegisteredClaimNames.Iss]));
        Assert.Equal(session.Id, query[JwtRegisteredClaimNames.Sid]);
    }
}
