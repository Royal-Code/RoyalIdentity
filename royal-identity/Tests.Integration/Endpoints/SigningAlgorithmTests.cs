using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Utils;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.Integration.Prepare;
using RealmModel = RoyalIdentity.Models.Realm;

namespace Tests.Integration.Endpoints;

public class SigningAlgorithmTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public SigningAlgorithmTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ClientCredentials_WhenNoSigningFilter_ShouldUseRealmDefaultAlgorithm()
    {
        var realm = await CreateRealmAsync();
        var scope = AddResourceServer(realm, "api", []);
        var clientId = AddClient(realm, [scope]);

        var token = await RequestClientCredentialsAccessTokenAsync(realm, clientId, scope);

        Assert.Equal(SecurityAlgorithms.EcdsaSha256, ReadJwt(token).Header.Alg);
    }

    [Fact]
    public async Task ClientCredentials_WhenClientSigningFilterAndNoResourceServerFilter_ShouldUseClientAlgorithm()
    {
        var realm = await CreateRealmAsync();
        var scope = AddResourceServer(realm, "api", []);
        var clientId = AddClient(realm, [scope], configureClient: client =>
        {
            client.AllowedAccessTokenSigningAlgorithms.Add(SecurityAlgorithms.RsaSha256);
        });

        var token = await RequestClientCredentialsAccessTokenAsync(realm, clientId, scope);

        Assert.Equal(SecurityAlgorithms.RsaSha256, ReadJwt(token).Header.Alg);
    }

    [Fact]
    public async Task ClientCredentials_WhenResourceServerSigningFilterExists_ShouldIgnoreClientFilter()
    {
        var realm = await CreateRealmAsync();
        var scope = AddResourceServer(realm, "api", [SecurityAlgorithms.EcdsaSha256]);
        var clientId = AddClient(realm, [scope], configureClient: client =>
        {
            client.AllowedAccessTokenSigningAlgorithms.Add(SecurityAlgorithms.RsaSha256);
        });

        var token = await RequestClientCredentialsAccessTokenAsync(realm, clientId, scope);

        Assert.Equal(SecurityAlgorithms.EcdsaSha256, ReadJwt(token).Header.Alg);
    }

    [Fact]
    public async Task ClientCredentials_WhenMultipleResourceServersHaveSigningFilters_ShouldUseIntersection()
    {
        var realm = await CreateRealmAsync();
        var firstScope = AddResourceServer(realm, "first", [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256]);
        var secondScope = AddResourceServer(realm, "second", [SecurityAlgorithms.EcdsaSha256]);
        var clientId = AddClient(realm, [firstScope, secondScope]);

        var token = await RequestClientCredentialsAccessTokenAsync(realm, clientId, $"{firstScope} {secondScope}");

        Assert.Equal(SecurityAlgorithms.EcdsaSha256, ReadJwt(token).Header.Alg);
    }

    [Fact]
    public async Task ClientCredentials_WhenMultipleResourceServersHaveIncompatibleSigningFilters_ShouldReturnInvalidRequest()
    {
        var realm = await CreateRealmAsync();
        var firstScope = AddResourceServer(realm, "first", [SecurityAlgorithms.RsaSha256]);
        var secondScope = AddResourceServer(realm, "second", [SecurityAlgorithms.EcdsaSha256]);
        var clientId = AddClient(realm, [firstScope, secondScope]);

        var client = factory.CreateClient();
        var response = await client.PostAsync(
            Oidc.Routes.BuildTokenUrl(realm.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Oidc.Token.Request.GrantType] = "client_credentials",
                [Oidc.Token.Request.ClientId] = clientId,
                [Oidc.Token.Request.ClientSecret] = $"{clientId}-secret",
                [Oidc.Token.Request.Scope] = $"{firstScope} {secondScope}"
            }));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_request", body);
        Assert.Contains("Signing algorithms requirements", body);
    }

    [Fact]
    public async Task AuthorizationCode_WhenAccessTokenUsesResourceServerFilter_IdTokenShouldUseClientIdentityTokenFilter()
    {
        var realm = await CreateRealmAsync();
        var scope = AddResourceServer(realm, "api", [SecurityAlgorithms.RsaSha256]);
        var clientId = AddClient(realm, [scope], allowedIdentityScopes: ["openid"], configureClient: client =>
        {
            client.RequireClientSecret = false;
            client.AllowedGrantTypes.Add("authorization_code");
            client.AllowedResponseTypes.Add("code");
            client.RedirectUris.Add("http://localhost:5000/**");
            client.AllowedIdentityTokenSigningAlgorithms.Add(SecurityAlgorithms.EcdsaSha256);
        });
        SeedAlice(realm);

        var storage = factory.Services.GetRequiredService<IStorage>();
        var resources = await storage.GetResourceStore(realm).FindResourcesByScopeAsync(
            ["openid", scope],
            onlyEnabled: true);
        var code = new RoyalIdentity.Models.Tokens.AuthorizationCode(
            clientId,
            SubjectFactory.CreateWithSession(storage, realm, MemoryStorage.AliceSubjectId, "Test Name", "admin"),
            "session",
            DateTime.UtcNow,
            300,
            resources,
            "http://localhost:5000/callback");

        await storage.GetAuthorizationCodeStore(realm).StoreAuthorizationCodeAsync(code, default);

        var client = factory.CreateClient();
        var response = await client.PostAsync(
            Oidc.Routes.BuildTokenUrl(realm.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Oidc.Token.Request.GrantType] = "authorization_code",
                [Oidc.Token.Request.Code] = code.Code,
                [Oidc.Token.Request.ClientId] = clientId,
                [Oidc.Token.Request.RedirectUri] = "http://localhost:5000/callback"
            }));

        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.Equal(SecurityAlgorithms.RsaSha256, ReadJwt(content!["access_token"].ToString()!).Header.Alg);
        Assert.Equal(SecurityAlgorithms.EcdsaSha256, ReadJwt(content["id_token"].ToString()!).Header.Alg);
    }

    private async Task<RealmModel> CreateRealmAsync()
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var suffix = CryptoRandom.CreateUniqueId(6, CryptoRandom.OutputFormat.Hex);
        var realm = await manager.CreateAsync($"signing-{suffix}", $"signing-{suffix}.test", $"Signing {suffix}");

        realm.Options.Keys.MainSigningCredentialsAlgorithm = SecurityAlgorithms.EcdsaSha256;
        await storage.Realms.SaveAsync(realm);

        var now = DateTime.UtcNow;
        var rsa = KeyParameters.Create(realm.Options.Keys, SecurityAlgorithms.RsaSha256);
        rsa.Created = now.AddMinutes(-2);
        var ecdsa = KeyParameters.Create(realm.Options.Keys, SecurityAlgorithms.EcdsaSha256);
        ecdsa.Created = now.AddMinutes(-1);

        var keyStore = storage.GetKeyStore(realm);
        await keyStore.AddKeyAsync(rsa, default);
        await keyStore.AddKeyAsync(ecdsa, default);

        return realm;
    }

    private string AddResourceServer(RealmModel realm, string name, IEnumerable<string> signingAlgorithms)
    {
        var suffix = CryptoRandom.CreateUniqueId(4, CryptoRandom.OutputFormat.Hex);
        var serverName = $"{name}-{suffix}";
        var scopeName = $"{serverName}:read";
        var server = new ResourceServer(ScopeVisibility.Public, serverName, $"{name} API", $"{name} API")
        {
            Scopes =
            [
                new Scope(ScopeVisibility.Public, scopeName, $"{name} read", $"Read {name}")
            ],
            AllowedAccessTokenSigningAlgorithms = [.. signingAlgorithms],
        };

        factory.Services.GetRequiredService<MemoryStorage>().GetRealmMemoryStore(realm).ResourceServers[serverName] = server;

        return scopeName;
    }

    private void SeedAlice(RealmModel realm)
    {
        factory.Services.GetRequiredService<MemoryStorage>().GetRealmMemoryStore(realm).UserAccounts["alice"] = new MemoryUserAccount
        {
            SubjectId = MemoryStorage.AliceSubjectId,
            Username = "alice",
            PasswordHash = PasswordHash.Create("alice"),
            DisplayName = "Alice",
            IsActive = true
        };
    }

    private string AddClient(
        RealmModel realm,
        IEnumerable<string> allowedScopes,
        IEnumerable<string>? allowedIdentityScopes = null,
        Action<Client>? configureClient = null)
    {
        var clientId = $"client-{CryptoRandom.CreateUniqueId(6, CryptoRandom.OutputFormat.Hex)}";
        var client = new Client
        {
            Realm = realm,
            Id = clientId,
            Name = $"Client {clientId}",
            RequireClientSecret = true,
            AllowedGrantTypes = ["client_credentials"],
            ClientSecrets = { new ClientSecret($"{clientId}-secret".Sha512()) },
        };

        foreach (var scope in allowedScopes)
            client.AllowedScopes.Add(scope);

        foreach (var identityScope in allowedIdentityScopes ?? [])
            client.AllowedIdentityScopes.Add(identityScope);

        configureClient?.Invoke(client);

        factory.Services.GetRequiredService<MemoryStorage>().GetRealmMemoryStore(realm).Clients[clientId] = client;

        return clientId;
    }

    private async Task<string> RequestClientCredentialsAccessTokenAsync(RealmModel realm, string clientId, string scopes)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync(
            Oidc.Routes.BuildTokenUrl(realm.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Oidc.Token.Request.GrantType] = "client_credentials",
                [Oidc.Token.Request.ClientId] = clientId,
                [Oidc.Token.Request.ClientSecret] = $"{clientId}-secret",
                [Oidc.Token.Request.Scope] = scopes
            }));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Token request failed with status {(int)response.StatusCode}: {body}");

        var content = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(content);

        return content![Oidc.Token.Response.AccessToken].GetString()!;
    }

    private static JwtSecurityToken ReadJwt(string token)
    {
        return new JwtSecurityTokenHandler().ReadJwtToken(token);
    }
}
