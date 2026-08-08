using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Utils;
using System.Net;
using System.Net.Http.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class RefreshTokenTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public RefreshTokenTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Post_WhenValidRefreshToken_ShouldReturnNewTokens()
    {
        // Arrange
        var clientId = "refresh_grant_type_client_3";
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, client =>
        {
            client.Name = "Demo Client";
            client.RequireClientSecret = false;
            client.AllowOfflineAccess = true;
            client.AllowedIdentityScopes.UnionWith(["openid", "profile", "email"]);
            client.AllowedResponseTypes.Add("code");
            client.AllowedGrantTypes.UnionWith(["code", "refresh_token"]);
            client.RedirectUris.UnionWith(["https://localhost:5000/callback", "https://localhost:5001/callback"]);
        });

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refresh_token = tokens.RefreshToken!;

        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refresh_token,
                    ["client_id"] = clientId,
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        Assert.True(content.ContainsKey("token_type"));
        Assert.True(content.ContainsKey("expires_in"));
        Assert.True(content.ContainsKey("scope"));
        Assert.True(content.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenDoNotHasRefreshTokenGrantTypeAllowed_ShouldReturnBadRequest()
    {
        // Arrange — dedicated client: offline_access allowed, but "refresh_token" grant type is not
        var clientId = "no_refresh_grant_type_client";
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, client =>
        {
            client.Name = "No Refresh Grant Client";
            client.RequireClientSecret = false;
            client.AllowOfflineAccess = true;
            client.AllowedIdentityScopes.UnionWith(["openid", "profile"]);
            client.AllowedResponseTypes.Add("code");
            client.AllowedGrantTypes.Add("authorization_code");
            client.RedirectUris.UnionWith(["https://localhost:5000/callback", "https://localhost:5001/callback"]);
        });

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refreshToken = tokens.RefreshToken!;

        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.NotNull(content);
        Assert.True(content.ContainsKey("error_description"));
        Assert.Equal("Client not authorized for refresh_token flow", content["error_description"].ToString());
    }

    [Fact]
    public async Task Post_WhenClientHasSecret_And_SecretNotInformed_ShouldReturnBadRequest()
    {
        // Arrange
        var clientId = "refresh_grant_type_client_1";
        var clientSecret = CryptoRandom.CreateUniqueId();
        var secretHash = clientSecret.Sha512();
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, client =>
        {
            client.Name = "Client with Secret";
            client.RequireClientSecret = true;
            client.RequirePkce = false;
            client.AllowOfflineAccess = true;
            client.AllowedIdentityScopes.UnionWith(["openid", "profile", "email"]);
            client.AllowedResponseTypes.Add("code");
            client.AllowedGrantTypes.UnionWith(["code", "refresh_token"]);
            client.RedirectUris.UnionWith(["https://localhost:5000/callback", "https://localhost:5001/callback"]);
            client.Secrets.Add(new ClientSecret(secretHash));
        });

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refresh_token = tokens.RefreshToken!;

        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refresh_token,
                    ["client_id"] = clientId,
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task Post_WhenValidRefreshToken_And_ValidSecret_ShouldReturnNewTokens()
    {
        // Arrange
        var clientId = "refresh_grant_type_client_2";
        var clientSecret = CryptoRandom.CreateUniqueId();
        var secretHash = clientSecret.Sha512();
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, client =>
        {
            client.Name = "Client with Secret";
            client.RequireClientSecret = true;
            client.RequirePkce = false;
            client.AllowOfflineAccess = true;
            client.AllowedIdentityScopes.UnionWith(["openid", "profile", "email"]);
            client.AllowedResponseTypes.Add("code");
            client.AllowedGrantTypes.UnionWith(["code", "refresh_token"]);
            client.RedirectUris.UnionWith(["https://localhost:5000/callback", "https://localhost:5001/callback"]);
            client.Secrets.Add(new ClientSecret(secretHash));
        });

        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var tokens = await client.GetTokensAsync(clientId: clientId);
        var refresh_token = tokens.RefreshToken!;

        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refresh_token,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        Assert.True(content.ContainsKey("token_type"));
        Assert.True(content.ContainsKey("expires_in"));
        Assert.True(content.ContainsKey("scope"));
        Assert.True(content.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenRefreshTokenHasResourceIndicator_ShouldPreserveAudience()
    {
        var (clientId, refreshToken, _) = await CreateRefreshTokenWithResourcesAsync(
            ["https://api.demo.local/apiserver"]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var accessToken = content!["access_token"].ToString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains("https://api.demo.local/apiserver", jwt.Audiences);
        Assert.DoesNotContain("apiserver", jwt.Audiences);
    }

    [Fact]
    public async Task Post_WhenRefreshTokenRequestsResourceSubset_ShouldSetSubsetAudience()
    {
        var ordersResource = AddOrdersResourceServer();
        var (clientId, refreshToken, _) = await CreateRefreshTokenWithResourcesAsync(
            ["https://api.demo.local/apiserver", ordersResource]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
                    ["resource"] = ordersResource
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var accessToken = content!["access_token"].ToString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains(ordersResource, jwt.Audiences);
        Assert.DoesNotContain("https://api.demo.local/apiserver", jwt.Audiences);
    }

    [Fact]
    public async Task Post_WhenRefreshTokenRequestsResourceSubsetWithApiScopes_ShouldDownscopeScopesAndAudience()
    {
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var ordersServer = $"orders-refresh-with-scope-{suffix}";
        var ordersScope = $"orders:read:{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";
        factory.Resources.SetResourceServer(factory.Handles.Demo.Id, new ResourceServer(
            ScopeVisibility.Public,
            ordersServer,
            "Orders API",
            "Orders API")
        {
            Scopes =
            [
                new Scope(ScopeVisibility.Public, ordersScope, "Orders read", "Read orders")
            ],
            ProtectedResources =
            [
                new ProtectedResource(ordersResource)
            ]
        });

        var (clientId, refreshToken, _) = await CreateRefreshTokenWithResourcesAsync(
            ["https://api.demo.local/apiserver", ordersResource],
            ["openid", "offline_access", "api:read", ordersScope]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
                    ["resource"] = ordersResource
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var scope = content!["scope"].ToString()!;
        Assert.Contains(ordersScope, scope);
        // identity scope and offline_access survive the downscope (scope axis, not the resource axis)
        Assert.Contains("openid", scope);
        Assert.Contains("offline_access", scope);
        Assert.DoesNotContain("api:read", scope);
        var accessToken = content["access_token"].ToString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains(ordersResource, jwt.Audiences);
        Assert.DoesNotContain("https://api.demo.local/apiserver", jwt.Audiences);
    }

    [Fact]
    public async Task Post_WhenRefreshTokenRequestsUnauthorizedResourceSubset_ShouldReturnInvalidTarget()
    {
        var ordersResource = AddOrdersResourceServer();
        var (clientId, refreshToken, _) = await CreateRefreshTokenWithResourcesAsync(
            ["https://api.demo.local/apiserver"]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = clientId,
                    ["resource"] = ordersResource
                }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidTarget);
    }

    private string AddOrdersResourceServer()
    {
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var ordersServer = $"orders-{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";

        factory.Resources.SetResourceServer(factory.Handles.Demo.Id, new ResourceServer(
            ScopeVisibility.Public,
            ordersServer,
            "Orders API",
            "Orders API")
        {
            ProtectedResources =
            [
                new ProtectedResource(ordersResource)
            ]
        });

        return ordersResource;
    }

    private async Task<(string ClientId, string RefreshToken, string AccessToken)> CreateRefreshTokenWithResourcesAsync(
        string[] resourceUris,
        string[]? scopeNames = null)
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var clientId = $"refresh-resource-client-{suffix}";

        var allowedResourceServers = factory.Resources.GetResourceServers(factory.Handles.Demo.Id)
            .Where(server => server.ProtectedResources.Any(resource => resourceUris.Contains(resource.ResourceUri)))
            .Select(server => server.Name)
            .ToArray();

        await factory.SaveClientAsync(factory.Handles.Demo, clientId, client =>
        {
            client.Name = "Refresh Resource Client";
            client.RequireClientSecret = false;
            client.RequirePkce = false;
            client.AllowOfflineAccess = true;
            client.AllowedGrantTypes.UnionWith(["authorization_code", "refresh_token"]);
            client.AllowedIdentityScopes.Add("openid");
            client.AllowedResponseTypes.Add("code");
            client.RedirectUris.Add("https://localhost:5000/callback");
            client.AllowedResourceServers.UnionWith(allowedResourceServers);
        });

        var code = await factory.WithStorageAsync(async storage =>
        {
            var resources = await storage.GetResourceStore(realm).FindRequestedResourcesAsync(
                scopeNames ?? ["openid", "offline_access"],
                resourceUris,
                onlyEnabled: true);

            var authorizationCode = new RoyalIdentity.Models.Tokens.AuthorizationCode(
                clientId,
                SubjectFactory.CreateWithSession(
                    storage, realm, factory.Handles.Alice.SubjectId, "Test Name", "admin"),
                DateTime.UtcNow,
                300,
                resources,
                "https://localhost:5000/callback");

            await storage.GetAuthorizationCodeStore(realm)
                .StoreAuthorizationCodeAsync(authorizationCode, default);
            return authorizationCode;
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "https://localhost:5000/callback"
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);

        return (
            clientId,
            content!["refresh_token"].ToString()!,
            content["access_token"].ToString()!);
    }
}
