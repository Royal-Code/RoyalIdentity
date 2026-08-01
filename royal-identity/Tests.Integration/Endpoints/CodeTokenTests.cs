// Ignore Spelling: Pkce

using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Utils;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class CodeTokenTests : IClassFixture<PersistentStorageAppFactory>
{
    private static readonly string[] scopeNames = ["openid", "profile"];

    private readonly PersistentStorageAppFactory factory;

    public CodeTokenTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    private async Task<RoyalIdentity.Models.Tokens.AuthorizationCode> CreateCodeAsync(
        string clientId,
        IEnumerable<string> scopes,
        IEnumerable<string>? resourceUris = null,
        Action<RoyalIdentity.Models.Tokens.AuthorizationCode>? configure = null)
    {
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var resourceStore = storage.GetResourceStore(realm);
        var resources = resourceUris is null
            ? await resourceStore.FindResourcesByScopeAsync(scopes, default)
            : await resourceStore.FindRequestedResourcesAsync(scopes, resourceUris, onlyEnabled: true);
        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            clientId,
            SubjectFactory.CreateWithSession(
                storage,
                realm,
                factory.Handles.Alice.SubjectId,
                "Test Name",
                "admin"),
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");
        configure?.Invoke(code);
        await storage.GetAuthorizationCodeStore(realm).StoreAuthorizationCodeAsync(code, default);
        return code;
    }

    private Task SaveAuthorizationClientAsync(
        string clientId,
        string? clientSecret = null,
        Action<TestClientBuilder>? configure = null)
    {
        return factory.SaveClientAsync(
            factory.Handles.Demo,
            clientId,
            configured =>
            {
                configured.RequireClientSecret = clientSecret is not null;
                configured.RequirePkce = false;
                configured.AllowOfflineAccess = true;
                configured.AllowedGrantTypes.Add("authorization_code");
                configured.AllowedIdentityScopes.UnionWith(["openid", "profile", "email"]);
                configured.AllowedResponseTypes.Add("code");
                configured.RedirectUris.UnionWith(
                    ["http://localhost:5000/**", "https://localhost:5001/**"]);
                if (clientSecret is not null)
                    configured.Secrets.Add(new ClientSecret(clientSecret.Sha512()));
                configure?.Invoke(configured);
            });
    }

    [Fact]
    public async Task Post_WhenValidCode_Must_GenerateToken()
    {
        // Arrange
        var code = await CreateCodeAsync(factory.Handles.DemoClient.ClientId, scopeNames);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url, 
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = factory.Handles.DemoClient.ClientId,
                    ["redirect_uri"] = "http://localhost:5000/callback"
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());

        Assert.NotNull(content);
        Assert.True(content.ContainsKey("access_token"));
        Assert.True(content.ContainsKey("token_type"));
        Assert.True(content.ContainsKey("expires_in"));
        Assert.True(content.ContainsKey("scope"));
        Assert.True(content.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenValidCode_WithPkce_Must_GenerateToken()
    {
        // Arrange
        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeChallenge = PkceHelper.GenerateStoredS256CodeChallengeHash(codeVerifier);
        var code = await CreateCodeAsync(
            factory.Handles.DemoClient.ClientId,
            scopeNames,
            configure: configured =>
        {
            configured.CodeChallenge = codeChallenge;
            configured.CodeChallengeMethod = "S256";
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = factory.Handles.DemoClient.ClientId,
                    ["redirect_uri"] = "http://localhost:5000/callback",
                    ["code_verifier"] = codeVerifier
                }));

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(token);
        Assert.True(token.ContainsKey("access_token"));
        Assert.True(token.ContainsKey("token_type"));
        Assert.True(token.ContainsKey("expires_in"));
        Assert.True(token.ContainsKey("scope"));
        Assert.True(token.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenNotValidCode_WithPkce_Must_BadRequest()
    {
        // Arrange
        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeChallenge = PkceHelper.GenerateStoredS256CodeChallengeHash(codeVerifier);
        _ = await CreateCodeAsync(
            factory.Handles.DemoClient.ClientId,
            scopeNames,
            configure: configured =>
        {
            configured.CodeChallenge = codeChallenge;
            configured.CodeChallengeMethod = "S256";
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = CryptoRandom.CreateUniqueId(),
                    ["client_id"] = factory.Handles.DemoClient.ClientId,
                    ["redirect_uri"] = "http://localhost:5000/callback",
                    ["code_verifier"] = codeVerifier
                }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenNotValidCode_Must_BadRequest()
    {
        // Arrange
        _ = await CreateCodeAsync(factory.Handles.DemoClient.ClientId, scopeNames);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = CryptoRandom.CreateUniqueId(),
                    ["client_id"] = factory.Handles.DemoClient.ClientId,
                    ["redirect_uri"] = "http://localhost:5000/callback",
                }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenValidCode_WithClientSecret_Must_GenerateToken()
    {
        // Arrange
        var clientId = "code_grant_type_client_1";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveAuthorizationClientAsync(clientId, clientSecret);
        var code = await CreateCodeAsync(clientId, scopeNames);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = "http://localhost:5000/callback"
                }));

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(token);
        Assert.True(token.ContainsKey("access_token"));
        Assert.True(token.ContainsKey("token_type"));
        Assert.True(token.ContainsKey("expires_in"));
        Assert.True(token.ContainsKey("scope"));
        Assert.True(token.ContainsKey("id_token"));
    }

    [Fact]
    public async Task Post_WhenValidCode_AndNoSecret_WithClientSecret_Must_BadRequest()
    {
        // Arrange
        var clientId = "code_grant_type_client_2";
        var clientSecret = CryptoRandom.CreateUniqueId();
        await SaveAuthorizationClientAsync(clientId, clientSecret);
        var code = await CreateCodeAsync(clientId, scopeNames);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback"
                }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WhenCodeHasResourceIndicator_ShouldSetAudienceToResourceUri()
    {
        var clientId = $"code-resource-client-{CryptoRandom.CreateUniqueId(4, OutputFormat.Hex)}";
        await SaveAuthorizationClientAsync(clientId, configure: configured =>
        {
            configured.AllowedResourceServers.Add("apiserver");
        });
        var code = await CreateCodeAsync(
            clientId,
            ["openid"],
            ["https://api.demo.local/apiserver"]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback"
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
    public async Task Post_WhenCodeTokenRequestUsesResourceSubset_ShouldSetSubsetAudience()
    {
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var ordersServer = $"orders-{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";
        factory.Resources.SetResourceServer(
            factory.Handles.Demo.Id,
            new ResourceServer(
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

        var clientId = $"code-subset-client-{suffix}";
        await SaveAuthorizationClientAsync(clientId, configure: configured =>
        {
            configured.Name = "Code Subset Client";
            configured.AllowedResourceServers.UnionWith(["apiserver", ordersServer]);
        });
        var code = await CreateCodeAsync(
            clientId,
            ["openid"],
            ["https://api.demo.local/apiserver", ordersResource]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback",
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
    public async Task Post_WhenCodeTokenRequestUsesResourceSubsetWithApiScopes_ShouldDownscopeScopesAndAudience()
    {
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var ordersServer = $"orders-with-scope-{suffix}";
        var ordersScope = $"orders:read:{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";
        factory.Resources.SetResourceServer(
            factory.Handles.Demo.Id,
            new ResourceServer(
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

        var clientId = $"code-subset-scopes-client-{suffix}";
        await SaveAuthorizationClientAsync(clientId, configure: configured =>
        {
            configured.Name = "Code Subset Scopes Client";
            configured.AllowedResourceServers.UnionWith(["apiserver", ordersServer]);
        });
        var code = await CreateCodeAsync(
            clientId,
            ["openid", "api:read", ordersScope],
            ["https://api.demo.local/apiserver", ordersResource]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback",
                    ["resource"] = ordersResource
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.Contains(ordersScope, content!["scope"].ToString());
        // identity scopes survive the downscope (they do not flow through the resource axis)
        Assert.Contains("openid", content["scope"].ToString());
        Assert.DoesNotContain("api:read", content["scope"].ToString());
        var accessToken = content["access_token"].ToString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains(ordersResource, jwt.Audiences);
        Assert.DoesNotContain("https://api.demo.local/apiserver", jwt.Audiences);
    }

    [Fact]
    public async Task Post_WhenCodeTokenResourceSubset_ShouldKeepScopesOfResourceServersWithoutProtectedResources()
    {
        // ADR-012 (subset): downscoping to a resource subset drops API scopes of resource-capable
        // resource servers left out of the subset, but keeps identity scopes and scopes of resource
        // servers that have no protected resources (they flow only through the scope axis).
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);

        // resource-capable RS kept in the subset
        var ordersServer = $"orders-{suffix}";
        var ordersScope = $"orders:read:{suffix}";
        var ordersResource = $"https://orders.demo.local/{suffix}";
        factory.Resources.SetResourceServer(
            factory.Handles.Demo.Id,
            new ResourceServer(ScopeVisibility.Public, ordersServer, "Orders API", "Orders API")
            {
                Scopes = [new Scope(ScopeVisibility.Public, ordersScope, "Orders read", "Read orders")],
                ProtectedResources = [new ProtectedResource(ordersResource)]
            });

        // RS without protected resources: its scope must survive the downscope (scope axis only)
        var plainServer = $"plain-{suffix}";
        var plainScope = $"plain:read:{suffix}";
        factory.Resources.SetResourceServer(
            factory.Handles.Demo.Id,
            new ResourceServer(ScopeVisibility.Public, plainServer, "Plain API", "Plain API")
            {
                Scopes = [new Scope(ScopeVisibility.Public, plainScope, "Plain read", "Read plain")]
            });

        var clientId = $"code-subset-mixed-client-{suffix}";
        await SaveAuthorizationClientAsync(clientId, configure: configured =>
        {
            configured.Name = "Code Subset Mixed Client";
            configured.AllowedResourceServers.UnionWith(["apiserver", ordersServer, plainServer]);
        });
        var code = await CreateCodeAsync(
            clientId,
            ["openid", "api:read", ordersScope, plainScope],
            ["https://api.demo.local/apiserver", ordersResource]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // narrow to the orders resource only
        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback",
                    ["resource"] = ordersResource
                }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        var scope = content!["scope"].ToString()!;
        // kept: identity scope, the in-subset resource-capable scope, the non-resource-capable scope
        Assert.Contains("openid", scope);
        Assert.Contains(ordersScope, scope);
        Assert.Contains(plainScope, scope);
        // dropped: the resource-capable scope whose RS was left out of the subset
        Assert.DoesNotContain("api:read", scope);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(content["access_token"].ToString()!);
        // aud: the requested resource + the plain RS audience (scope axis); not the dropped apiserver resource
        Assert.Contains(ordersResource, jwt.Audiences);
        Assert.Contains(plainServer, jwt.Audiences);
        Assert.DoesNotContain("https://api.demo.local/apiserver", jwt.Audiences);
    }

    [Fact]
    public async Task Post_WhenCodeTokenRequestUsesUnauthorizedResourceSubset_ShouldReturnInvalidTarget()
    {
        var suffix = CryptoRandom.CreateUniqueId(4, OutputFormat.Hex);
        var clientId = $"code-unauthorized-subset-client-{suffix}";
        await SaveAuthorizationClientAsync(clientId, configure: configured =>
        {
            configured.Name = "Code Unauthorized Subset Client";
            configured.AllowedResourceServers.Add("apiserver");
        });
        var code = await CreateCodeAsync(
            clientId,
            ["openid"],
            ["https://api.demo.local/apiserver"]);

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        var response = await client.PostAsync(url,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code.Code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = "http://localhost:5000/callback",
                    ["resource"] = "https://unknown.example.test/resource"
                }));

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidTarget);
    }
}
