using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using System.Net;
using System.Web;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class EndSessionTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public EndSessionTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_WhenValidIdToken_MustRedirectWithLogoutMessage()
    {
        // Arrange
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        });
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync();
        var idToken = tokens.IdentityToken!;

        var messageStorage = factory.Services.GetRequiredService<IMessageStore>();

        // Act
        var path = Oidc.Routes.BuildEndSessionUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("id_token_hint", idToken);

        var response = await client.GetAsync(path);
        
        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var location = response.Headers.Location;
        Assert.NotNull(location);

        var query = HttpUtility.ParseQueryString(location.Query);
        var queryLogoutId = query.GetValues("logoutId");

        Assert.NotNull(queryLogoutId);
        Assert.Single(queryLogoutId);

        var logoutId = queryLogoutId[0];
        var message = await messageStorage.ReadAsync<LogoutMessage>(logoutId, default);
        Assert.NotNull(message);
        
        var payload = message.Data;
        Assert.NotNull(payload);

        Assert.NotNull(payload.SessionId);
        Assert.True(payload.ShowSignoutPrompt);
        Assert.NotNull(payload.ClientName);
    }

    [Fact]
    public async Task Get_WhenNoHint_MustRedirectWithLogoutMessage()
    {
        // Arrange
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        });
        await client.LoginAliceAsync();

        var messageStorage = factory.Services.GetRequiredService<IMessageStore>();

        var url = Oidc.Routes.BuildEndSessionUrl(MemoryStorage.DemoRealm.Path);

        // Act
        var response = await client.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var location = response.Headers.Location;
        Assert.NotNull(location);

        var query = HttpUtility.ParseQueryString(location.Query);
        var queryLogoutId = query.GetValues("logoutId");

        Assert.NotNull(queryLogoutId);
        Assert.Single(queryLogoutId);

        var logoutId = queryLogoutId[0];
        var message = await messageStorage.ReadAsync<LogoutMessage>(logoutId, default);
        Assert.NotNull(message);

        var payload = message.Data;
        Assert.NotNull(payload);

        Assert.NotNull(payload.SessionId);
        Assert.True(payload.ShowSignoutPrompt);
        Assert.Null(payload.ClientName);
    }

    [Fact]
    public async Task Get_WhenClientAllowLogoutWithoutUserConfirmation_MustNotShowSignoutPrompt()
    {
        // Arrange
        var storage = factory.Services.GetRequiredService<MemoryStorage>();

        var clientId = "endsession_client_1";
        storage.GetDemoRealmStore().Clients.TryAdd(clientId, new RoyalIdentity.Models.Client()
        {
            Realm = MemoryStorage.DemoRealm,
            Id = clientId,
            Name = "Client Allow Logout Without User Confirmation",
            AllowLogoutWithoutUserConfirmation = true,
            RequireClientSecret = true,
            RequirePkce = false,
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", "email" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" },
            PostLogoutRedirectUris = { "http://localhost:5000/**", "https://localhost:5001/**" }
        });

        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions()
        {
            AllowAutoRedirect = false
        });
        await client.LoginAliceAsync();
        await client.GetAuthorizeAsync(clientId: clientId); // ensures that the client_id is added to the user's session.
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var idToken = tokens.IdentityToken!;

        var messageStorage = factory.Services.GetRequiredService<IMessageStore>();

        // Act
        var path = Oidc.Routes.BuildEndSessionUrl(MemoryStorage.DemoRealm.Path)
            .AddQueryString("id_token_hint", idToken)
            .AddQueryString("client_id", clientId)
            .AddQueryString("post_logout_redirect_uri", "https://localhost:5001/signout-callback");

        var response = await client.GetAsync(path);

        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var location = response.Headers.Location;
        Assert.NotNull(location);

        var query = HttpUtility.ParseQueryString(location.Query);
        var queryLogoutId = query.GetValues("logoutId");

        Assert.NotNull(queryLogoutId);
        Assert.Single(queryLogoutId);

        var logoutId = queryLogoutId[0];
        var message = await messageStorage.ReadAsync<LogoutMessage>(logoutId, default);
        Assert.NotNull(message);

        var payload = message.Data;
        Assert.NotNull(payload);

        Assert.NotNull(payload.SessionId);
        Assert.False(payload.ShowSignoutPrompt);
        Assert.Equal("Client Allow Logout Without User Confirmation", payload.ClientName);
        Assert.Equal("https://localhost:5001/signout-callback", payload.PostLogoutRedirectUri);
    }
}
