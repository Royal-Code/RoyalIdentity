using System.Net;
using System.Net.Http.Json;
using RoyalIdentity.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class ClientTokenTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public ClientTokenTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    private Task SaveClientAsync(
        string clientId,
        string clientSecret,
        Action<TestClientBuilder>? configure = null)
    {
        return factory.SaveClientAsync(
            factory.Handles.Demo,
            clientId,
            configured =>
            {
                configured.ClientType = ClientType.Confidential;
                configured.RequireClientSecret = true;
                configured.AllowedGrantTypes.Clear();
                configured.AllowedGrantTypes.Add("client_credentials");
                configured.Secrets.Add(new ClientSecret(clientSecret.Sha512()));
                configure?.Invoke(configured);
            });
    }

    private void SetResourceServer(ResourceServer server)
        => factory.Resources.SetResourceServer(factory.Handles.Demo.Id, server);

    [Fact]
    public async Task Post_WhenValidClientCredentials_ShouldReturnNewTokens()
    {
        // Arrange
        var clientId = "client_credentials_client_1";
        var clientSecret = "client_credentials_client_1_secret";
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Demo Client";
            configured.AllowOfflineAccess = false;
            configured.AllowedIdentityScopes.UnionWith(["openid", "profile", "email"]);
            configured.AllowedScopes.Add("api");
            configured.AllowedResponseTypes.Add("code");
            configured.RedirectUris.UnionWith(
                ["http://localhost:5000/**", "https://localhost:5001/**"]);
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api"
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        Assert.True(content.ContainsKey("token_type"));
        Assert.True(content.ContainsKey("expires_in"));
        Assert.True(content.ContainsKey("scope"));
        Assert.False(content.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenAllowAllResourceServers_ShouldAuthorizeAnyScope()
    {
        // Full Scope Allowed (ADR-011): the client lists no scope/resource server, only AllowAllResourceServers.
        var clientId = "full_scope_client";
        var clientSecret = "full_scope_client_secret";
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Full Scope Client";
            configured.AllowAllResourceServers = true;
            configured.AllowedResponseTypes.Add("code");
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api:read api:write"
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        var scope = content!["scope"].ToString();
        Assert.Contains("api:read", scope);
        Assert.Contains("api:write", scope);
    }

    [Fact]
    public async Task Post_WhenScopeNotAllowed_ShouldReturnInvalidScope()
    {
        // The client is allowed no API scope and has no Full Scope Allowed: requesting "api" is invalid_scope.
        var clientId = "no_api_client";
        var clientSecret = "no_api_client_secret";
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "No Api Client";
            configured.AllowedResponseTypes.Add("code");
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api"
                }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidScope);
    }

    [Fact]
    public async Task Post_WhenClientCredentialsRequestsOfflineAccess_ShouldReturnInvalidScope()
    {
        var clientId = $"offline-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Offline Client Credentials Client";
            configured.AllowOfflineAccess = true;
            configured.AllowedScopes.Add("api");
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api offline_access"
                }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidScope);
        Assert.DoesNotContain("access_token", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_WhenClientCredentialsOmitsScope_ShouldReturnDefaultAllowedScopes()
    {
        var clientId = $"default-scope-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Default Scope Client";
            configured.AllowedScopes.Add("api");
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        Assert.Equal("api", content["scope"].ToString());
    }

    [Fact]
    public async Task Post_WithResourceIndicator_ShouldSetAudienceToResourceUri()
    {
        // RFC 8707: requesting a resource indicator emits its URI as the aud and suppresses the legacy RS audience.
        var clientId = $"resource-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Resource Client";
            configured.AllowedResourceServers.Add("apiserver");
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api",
                    ["resource"] = "https://api.demo.local/apiserver"
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
    public async Task Post_WithUnknownResourceIndicator_ShouldReturnInvalidTarget()
    {
        var clientId = $"unknown-resource-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Unknown Resource Client";
            configured.AllowedResourceServers.Add("apiserver");
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api",
                    ["resource"] = "https://unknown.example/api"
                }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidTarget);
    }

    [Fact]
    public async Task Post_WithResourceNotAllowed_ShouldReturnInvalidTarget()
    {
        // Audience-only request for a resource whose resource server is not in AllowedResourceServers.
        var clientId = $"no-resource-client-{CryptoRandom.CreateUniqueId(6)}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "No Resource Client";
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["resource"] = "https://api.demo.local/apiserver"
                }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidTarget);
    }

    [Fact]
    public async Task Post_WithMultipleResourceIndicators_ShouldSetAllResourceAudiences()
    {
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var ordersServer = $"orders-{suffix}";
        var ordersScope = $"orders:read:{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";

        SetResourceServer(new ResourceServer(
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

        var clientId = $"multi-resource-client-{suffix}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Multi Resource Client";
            configured.AllowedResourceServers.UnionWith(["apiserver", ordersServer]);
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
            [
                new("grant_type", "client_credentials"),
                new("client_id", clientId),
                new("client_secret", clientSecret),
                new("resource", "https://api.demo.local/apiserver"),
                new("resource", ordersResource),
            ]));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var accessToken = content!["access_token"].ToString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains("https://api.demo.local/apiserver", jwt.Audiences);
        Assert.Contains(ordersResource, jwt.Audiences);
        Assert.DoesNotContain("apiserver", jwt.Audiences);
        Assert.DoesNotContain(ordersServer, jwt.Audiences);
    }

    [Fact]
    public async Task Post_WithScopeAndResourceFromDifferentResourceCapableServer_ShouldReturnInvalidTarget()
    {
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var ordersServer = $"orders-{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";

        SetResourceServer(new ResourceServer(
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

        var clientId = $"mismatch-resource-client-{suffix}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Mismatch Resource Client";
            configured.AllowedResourceServers.UnionWith(["apiserver", ordersServer]);
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = "api",
                    ["resource"] = ordersResource
                }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidTarget);
    }

    [Fact]
    public async Task Post_WhenResourceServerDisallowsScopeRequests_RequestingItsScope_ShouldReturnInvalidScope()
    {
        // ADR-012: a resource server with AllowScopeRequests = false cannot have its scopes requested
        // via the scope parameter, even when the client is otherwise allowed the resource server.
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var serverName = $"audience-only-{suffix}";
        var scopeName = $"{serverName}:read";

        SetResourceServer(new ResourceServer(
            ScopeVisibility.Public, serverName, "Audience Only API", "Audience Only API")
        {
            AllowScopeRequests = false,
            Scopes = [new Scope(ScopeVisibility.Public, scopeName, "read", "read")]
        });

        var clientId = $"audience-only-scope-client-{suffix}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Audience Only Scope Client";
            configured.AllowedResourceServers.Add(serverName);
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = scopeName
                }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidScope);
    }

    [Fact]
    public async Task Post_WhenResourceServerDisallowsScopeRequests_StillReachableViaResourceIndicator()
    {
        // ADR-012: an audience-only resource server (AllowScopeRequests = false) remains reachable via
        // the resource parameter — the resource axis is independent of the scope gate.
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var serverName = $"audience-only-reachable-{suffix}";
        var scopeName = $"{serverName}:read";
        var resourceUri = $"https://audience-only-{suffix}.demo.local/api";

        SetResourceServer(new ResourceServer(
            ScopeVisibility.Public, serverName, "Audience Only API", "Audience Only API")
        {
            AllowScopeRequests = false,
            Scopes = [new Scope(ScopeVisibility.Public, scopeName, "read", "read")],
            ProtectedResources = [new ProtectedResource(resourceUri)]
        });

        var clientId = $"audience-only-resource-client-{suffix}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Audience Only Resource Client";
            configured.AllowedResourceServers.Add(serverName);
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["resource"] = resourceUri
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(content!["access_token"].ToString()!);
        Assert.Contains(resourceUri, jwt.Audiences);
    }

    [Fact]
    public async Task Post_WithResourceIndicatorOfDisabledResourceServer_ShouldReturnInvalidTarget()
    {
        // ADR-012: a ProtectedResource availability derives from the owning resource server's Enabled.
        // A disabled resource server makes its resources unavailable -> invalid_target.
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var serverName = $"disabled-rs-{suffix}";
        var resourceUri = $"https://disabled-{suffix}.demo.local/api";

        SetResourceServer(new ResourceServer(
            ScopeVisibility.Public, serverName, "Disabled API", "Disabled API")
        {
            Enabled = false,
            ProtectedResources = [new ProtectedResource(resourceUri)]
        });

        var clientId = $"disabled-rs-client-{suffix}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Disabled RS Client";
            configured.AllowedResourceServers.Add(serverName);
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["resource"] = resourceUri
                }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidTarget);
    }

    [Fact]
    public async Task Post_WhenClientOmitsScope_DefaultScopesShouldExcludeResourceServersThatDisallowScopeRequests()
    {
        // ADR-012: when client_credentials omits scope, the default scopes are the requestable scopes of
        // the client's allowed resource servers; audience-only servers (AllowScopeRequests = false) are excluded.
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);

        var normalServer = $"normal-{suffix}";
        var normalScope = $"{normalServer}:read";
        SetResourceServer(new ResourceServer(
            ScopeVisibility.Public, normalServer, "Normal API", "Normal API")
        {
            Scopes = [new Scope(ScopeVisibility.Public, normalScope, "read", "read")]
        });

        var audienceOnlyServer = $"audience-only-default-{suffix}";
        var audienceOnlyScope = $"{audienceOnlyServer}:read";
        SetResourceServer(new ResourceServer(
            ScopeVisibility.Public, audienceOnlyServer, "Audience Only API", "Audience Only API")
        {
            AllowScopeRequests = false,
            Scopes = [new Scope(ScopeVisibility.Public, audienceOnlyScope, "read", "read")]
        });

        var clientId = $"default-scopes-client-{suffix}";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveClientAsync(clientId, clientSecret, configured =>
        {
            configured.Name = "Default Scopes Client";
            configured.AllowedResourceServers.UnionWith([normalServer, audienceOnlyServer]);
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // no scope parameter -> default scopes
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var scope = content!["scope"].ToString()!;
        Assert.Contains(normalScope, scope);
        Assert.DoesNotContain(audienceOnlyScope, scope);
    }
}
