using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class RedirectUriPolicyTests : IClassFixture<PersistentStorageAppFactory>
{
    private const string ExactRedirectUri = "https://client.example/callback";

    private readonly PersistentStorageAppFactory factory;

    public RedirectUriPolicyTests(PersistentStorageAppFactory factory) => this.factory = factory;

    [Fact]
    public async Task Authorize_DefaultPolicyRequiresAnOrdinalExactRedirectUri()
    {
        var realm = await CreateRealmAsync();
        var clientId = await SaveAuthorizationCodeClientAsync(realm, [ExactRedirectUri]);

        var exact = await AuthorizeAsync(realm, clientId, ExactRedirectUri);
        var differentCase = await AuthorizeAsync(realm, clientId, "https://CLIENT.example/callback");

        Assert.Equal(HttpStatusCode.Found, exact.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, differentCase.StatusCode);
        Assert.Null(differentCase.Headers.Location);
    }

    [Fact]
    public async Task Authorize_OrdinalIgnoreCaseIsAnExplicitRealmOptIn()
    {
        var realm = await CreateRealmAsync(options =>
            options.RedirectUriValidation.Comparison = RedirectUriComparison.OrdinalIgnoreCase);
        var clientId = await SaveAuthorizationCodeClientAsync(realm, [ExactRedirectUri]);

        var response = await AuthorizeAsync(realm, clientId, "https://CLIENT.example/callback");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_WildcardPolicyIsIsolatedBetweenRealms()
    {
        const string pattern = "https://*.client.example/callback";
        const string requested = "https://tenant.client.example/callback";
        var strictRealm = await CreateRealmAsync();
        var relaxedRealm = await CreateRealmAsync(options => options.RedirectUriValidation.AllowWildcard = true);
        var strictClient = await SaveAuthorizationCodeClientAsync(strictRealm, [pattern]);
        var relaxedClient = await SaveAuthorizationCodeClientAsync(relaxedRealm, [pattern]);

        var strict = await AuthorizeAsync(strictRealm, strictClient, requested);
        var relaxed = await AuthorizeAsync(relaxedRealm, relaxedClient, requested);

        Assert.Equal(HttpStatusCode.BadRequest, strict.StatusCode);
        Assert.Equal(HttpStatusCode.Found, relaxed.StatusCode);
    }

    [Theory]
    [InlineData("http://client.example/callback")]
    [InlineData("urn:client:callback")]
    [InlineData("/relative/callback")]
    [InlineData("https://client.example/callback#fragment")]
    public async Task Authorize_RejectsUnsafeRequestedRedirectUris(string redirectUri)
    {
        var realm = await CreateRealmAsync();
        var clientId = await SaveAuthorizationCodeClientAsync(realm, [ExactRedirectUri]);

        var response = await AuthorizeAsync(realm, clientId, redirectUri);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AuthorizationCodeRedemptionUsesTheSameStrictPolicyBeforeLookingUpTheCode()
    {
        var response = await factory.CreateClient().PostAsync(
            Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Oidc.Token.Request.GrantType] = OpenIdConnectGrantTypes.AuthorizationCode,
                [Oidc.Token.Request.Code] = "code-that-does-not-exist",
                [Oidc.Token.Request.ClientId] = factory.Handles.DemoClient.ClientId,
                [Oidc.Token.Request.RedirectUri] = "https://LOCALHOST:5000/callback",
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task EndSessionUsesTheSameStrictPolicyForPostLogoutRedirectUri()
    {
        var clientId = $"redirect-logout-{Guid.NewGuid():N}";
        const string postLogoutRedirectUri = "https://client.example/signed-out";
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, client =>
        {
            client.RequireClientSecret = false;
            client.RequirePkce = false;
            client.AllowedGrantTypes.UnionWith(
                [OpenIdConnectGrantTypes.AuthorizationCode, OpenIdConnectGrantTypes.RefreshToken]);
            client.AllowedResponseTypes.Add(Oidc.ResponseTypes.Code);
            client.AllowedIdentityScopes.Add(Server.StandardScopes.OpenId);
            client.RedirectUris.Add("https://localhost:5000/callback");
            client.PostLogoutRedirectUris.Add(postLogoutRedirectUri);
        });

        var client = CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId, Server.StandardScopes.OpenId);

        var accepted = await client.GetAsync(BuildEndSessionUrl(
            clientId, tokens.IdentityToken!, postLogoutRedirectUri));
        var rejected = await client.GetAsync(BuildEndSessionUrl(
            clientId, tokens.IdentityToken!, "https://CLIENT.example/signed-out"));

        Assert.Equal(HttpStatusCode.Found, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Null(rejected.Headers.Location);
    }

    private async Task<TestRealmHandle> CreateRealmAsync(Action<RealmOptions>? configure = null)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var path = $"redirect-{Guid.NewGuid():N}";
        var realm = await manager.CreateAsync(path, $"{path}.test", "Redirect policy realm");
        configure?.Invoke(realm.Options);
        await scope.ServiceProvider.GetRequiredService<IStorage>().Realms.SaveAsync(realm, default);
        await factory.RefreshConfigurationAsync();
        return new TestRealmHandle(realm.Id, realm.Path);
    }

    private async Task<string> SaveAuthorizationCodeClientAsync(
        TestRealmHandle realm,
        IEnumerable<string> redirectUris)
    {
        var clientId = $"redirect-client-{Guid.NewGuid():N}";
        await factory.SaveClientAsync(realm, clientId, client =>
        {
            client.RequireClientSecret = false;
            client.RequirePkce = true;
            client.AllowedGrantTypes.Add(OpenIdConnectGrantTypes.AuthorizationCode);
            client.AllowedResponseTypes.Add(Oidc.ResponseTypes.Code);
            client.AllowedIdentityScopes.Add(Server.StandardScopes.OpenId);
            client.RedirectUris.UnionWith(redirectUris);
        });
        return clientId;
    }

    private Task<HttpResponseMessage> AuthorizeAsync(
        TestRealmHandle realm,
        string clientId,
        string redirectUri)
    {
        var path = Oidc.Routes.BuildAuthorizeUrl(realm.Path)
            .AddQueryString(Oidc.Authorize.Request.ClientId, clientId)
            .AddQueryString(Oidc.Authorize.Request.ResponseType, Oidc.ResponseTypes.Code)
            .AddQueryString(Oidc.Authorize.Request.Scope, Server.StandardScopes.OpenId)
            .AddQueryString(Oidc.Authorize.Request.RedirectUri, redirectUri)
            .AddQueryString(Oidc.Authorize.Request.CodeChallenge, new string('a', 43))
            .AddQueryString(Oidc.Authorize.Request.CodeChallengeMethod, Oidc.CodeChallenge.Methods.Sha256);
        return CreateClient().GetAsync(path);
    }

    private static string BuildEndSessionUrl(string clientId, string idToken, string redirectUri)
        => Oidc.Routes.BuildEndSessionUrl("demo")
            .AddQueryString(Oidc.EndSession.Request.IdTokenHint, idToken)
            .AddQueryString(Oidc.EndSession.Request.ClientId, clientId)
            .AddQueryString(Oidc.EndSession.Request.PostLogoutRedirectUri, redirectUri);

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}
