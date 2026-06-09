using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Realm;

public class RealmOptionsPhase4Tests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public RealmOptionsPhase4Tests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task TokenFormat_RealmSpecificJwtType()
    {
        var realmA = await CreateRealmAsync("token-typ-a");
        var realmB = await CreateRealmAsync("token-typ-b");
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var clientA = $"typ-a-{CryptoRandom.CreateUniqueId(6)}";
        var clientB = $"typ-b-{CryptoRandom.CreateUniqueId(6)}";
        var secretA = CryptoRandom.CreateUniqueId();
        var secretB = CryptoRandom.CreateUniqueId();

        realmA.Options.AccessTokenJwtType = "realm-a+jwt";
        realmB.Options.AccessTokenJwtType = "realm-b+jwt";
        await storage.Realms.SaveAsync(realmA);
        await storage.Realms.SaveAsync(realmB);

        AddClient(memoryStorage, realmA, clientA, secretA);
        AddClient(memoryStorage, realmB, clientB, secretB);

        var tokenA = await RequestAccessTokenAsync(realmA, clientA, secretA, "api");
        var tokenB = await RequestAccessTokenAsync(realmB, clientB, secretB, "api");

        Assert.Equal("realm-a+jwt", ReadJwt(tokenA).Header.Typ);
        Assert.Equal("realm-b+jwt", ReadJwt(tokenB).Header.Typ);
    }

    [Fact]
    public async Task TokenFormat_RealmSpecificScopeSerialization()
    {
        var arrayRealm = await CreateRealmAsync("token-scope-array");
        var stringRealm = await CreateRealmAsync("token-scope-string");
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var arrayClient = $"scope-array-{CryptoRandom.CreateUniqueId(6)}";
        var stringClient = $"scope-string-{CryptoRandom.CreateUniqueId(6)}";
        var arraySecret = CryptoRandom.CreateUniqueId();
        var stringSecret = CryptoRandom.CreateUniqueId();

        arrayRealm.Options.EmitScopesAsSpaceDelimitedStringInJwt = false;
        stringRealm.Options.EmitScopesAsSpaceDelimitedStringInJwt = true;
        await storage.Realms.SaveAsync(arrayRealm);
        await storage.Realms.SaveAsync(stringRealm);

        AddClient(memoryStorage, arrayRealm, arrayClient, arraySecret);
        AddClient(memoryStorage, stringRealm, stringClient, stringSecret);

        var arrayToken = await RequestAccessTokenAsync(arrayRealm, arrayClient, arraySecret, "api:read api:write");
        var stringToken = await RequestAccessTokenAsync(stringRealm, stringClient, stringSecret, "api:read api:write");

        var arrayScope = ReadScopePayload(arrayToken);
        Assert.Equal(JsonValueKind.Array, arrayScope.ValueKind);
        Assert.Equal(
            ["api:read", "api:write"],
            arrayScope.EnumerateArray().Select(scope => scope.GetString()).Order());

        var stringScope = ReadScopePayload(stringToken);
        Assert.Equal(JsonValueKind.String, stringScope.ValueKind);
        Assert.Equal(
            ["api:read", "api:write"],
            stringScope.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order());
    }

    [Fact]
    public async Task TokenValidator_UsesRealmSpecificJwtType()
    {
        var realm = await CreateRealmAsync("token-validator");
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var clientId = $"validator-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();

        realm.Options.AccessTokenJwtType = "realm-token+jwt";
        await storage.Realms.SaveAsync(realm);

        AddClient(memoryStorage, realm, clientId, secret);
        var token = await RequestAccessTokenAsync(realm, clientId, secret, "api");

        using var scope = factory.Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<ITokenValidator>();

        var accepted = await validator.ValidateJwtAccessTokenAsync(realm, token);

        realm.Options.AccessTokenJwtType = "other-token+jwt";
        await storage.Realms.SaveAsync(realm);

        var rejected = await validator.ValidateJwtAccessTokenAsync(realm, token);

        Assert.True(accepted.IsValid);
        Assert.True(rejected.HasError);
        Assert.Equal(Oidc.ProtectedResource.Errors.InvalidToken, rejected.Error.Error);
        Assert.Equal("invalid JWT token type", rejected.Error.ErrorDescription);
    }

    [Fact]
    public void RealmOptions_CopyFromServer_PropagatesPhase4Values()
    {
        var serverOptions = new ServerOptions
        {
            AccessTokenJwtType = "server-token+jwt",
            EmitScopesAsSpaceDelimitedStringInJwt = true
        };

        var realmOptions = new RealmOptions(serverOptions);

        Assert.Equal("server-token+jwt", realmOptions.AccessTokenJwtType);
        Assert.True(realmOptions.EmitScopesAsSpaceDelimitedStringInJwt);
    }

    private async Task<RoyalIdentity.Models.Realm> CreateRealmAsync(string prefix)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var keyManager = scope.ServiceProvider.GetRequiredService<IKeyManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var path = $"{prefix}-{suffix}";

        var realm = await manager.CreateAsync(path, $"{path}.test", $"Test Realm {suffix}");
        await keyManager.CreateSigningCredentialsAsync(realm, default);

        return realm;
    }

    private async Task<string> RequestAccessTokenAsync(
        RoyalIdentity.Models.Realm realm,
        string clientId,
        string secret,
        string scopes)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsync(
            Oidc.Routes.BuildTokenUrl(realm.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Oidc.Token.Request.GrantType] = "client_credentials",
                [Oidc.Token.Request.ClientId] = clientId,
                [Oidc.Token.Request.ClientSecret] = secret,
                [Oidc.Token.Request.Scope] = scopes
            }));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Token request failed with status {(int)response.StatusCode}: {body}");

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();

        return content.GetProperty(Oidc.Token.Response.AccessToken).GetString()!;
    }

    private static void AddClient(
        MemoryStorage storage,
        RoyalIdentity.Models.Realm realm,
        string clientId,
        string secret)
    {
        storage.GetRealmMemoryStore(realm).Clients[clientId] = new Client
        {
            Realm = realm,
            Id = clientId,
            Name = $"Token Format Client {clientId}",
            RequireClientSecret = true,
            AllowedGrantTypes = ["client_credentials"],
            AllowedScopes = { "api", "api:read", "api:write" },
            AllowedResponseTypes = { "code" },
            ClientSecrets = { new ClientSecret(secret.Sha512()) }
        };
    }

    private static JwtSecurityToken ReadJwt(string token)
    {
        return new JwtSecurityTokenHandler().ReadJwtToken(token);
    }

    private static JsonElement ReadScopePayload(string token)
    {
        var payload = token.Split('.')[1];
        var json = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(payload));
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty(Jwt.ClaimTypes.Scope).Clone();
    }
}
