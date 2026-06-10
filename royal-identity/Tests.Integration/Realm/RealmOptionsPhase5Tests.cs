using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Realm;

public class RealmOptionsPhase5Tests : IClassFixture<AppFactory>
{
    private const string AccessControlAllowOrigin = "Access-Control-Allow-Origin";
    private const string AccessControlAllowMethods = "Access-Control-Allow-Methods";
    private const string AccessControlAllowHeaders = "Access-Control-Allow-Headers";
    private const string Origin = "Origin";
    private const string Vary = "Vary";

    private readonly AppFactory factory;

    public RealmOptionsPhase5Tests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Cors_Preflight_WhenOriginAllowedByRealm_ReturnsCorsHeaders()
    {
        var realm = await CreateRealmAsync("cors-realm");
        var storage = factory.Services.GetRequiredService<IStorage>();
        const string origin = "https://app.example";

        realm.Options.Cors.Enabled = true;
        realm.Options.Cors.AllowedOrigins.Add(origin);
        await storage.Realms.SaveAsync(realm);

        var response = await SendPreflightAsync(
            Oidc.Routes.BuildTokenUrl(realm.Path),
            origin,
            HttpMethods.Post,
            "content-type, authorization");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, GetHeader(response, AccessControlAllowOrigin));
        Assert.Equal(HttpMethods.Post, GetHeader(response, AccessControlAllowMethods));
        Assert.Equal("content-type, authorization", GetHeader(response, AccessControlAllowHeaders));
        Assert.Contains(Origin, GetHeader(response, Vary)!.Split(',', StringSplitOptions.TrimEntries));
    }

    [Fact]
    public async Task Cors_Preflight_WhenOriginAllowedOnlyInOtherRealm_IsRejected()
    {
        var realmA = await CreateRealmAsync("cors-a");
        var realmB = await CreateRealmAsync("cors-b");
        var storage = factory.Services.GetRequiredService<IStorage>();
        const string origin = "https://app.example";

        realmA.Options.Cors.Enabled = true;
        realmA.Options.Cors.AllowedOrigins.Add(origin);
        realmB.Options.Cors.Enabled = true;
        await storage.Realms.SaveAsync(realmA);
        await storage.Realms.SaveAsync(realmB);

        var response = await SendPreflightAsync(
            Oidc.Routes.BuildTokenUrl(realmB.Path),
            origin,
            HttpMethods.Post,
            "content-type");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(GetHeader(response, AccessControlAllowOrigin));
        Assert.Contains(Origin, GetHeader(response, Vary)!.Split(',', StringSplitOptions.TrimEntries));
    }

    [Fact]
    public async Task Cors_ActualRequest_WhenOriginAllowedByClient_ReturnsCorsHeaders()
    {
        var realm = await CreateRealmAsync("cors-client");
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var clientId = $"cors-client-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();
        const string origin = "https://client.example";

        realm.Options.Cors.Enabled = true;
        await storage.Realms.SaveAsync(realm);
        AddClient(memoryStorage, realm, clientId, secret, allowedCorsOrigin: origin);

        var response = await SendTokenRequestAsync(realm, clientId, "wrong-secret", origin);

        Assert.Equal(origin, GetHeader(response, AccessControlAllowOrigin));
    }

    [Fact]
    public async Task Cors_DoesNotInferRedirectUris_AsAllowedCorsOrigins()
    {
        var realm = await CreateRealmAsync("cors-redirect");
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var clientId = $"cors-redirect-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();
        const string origin = "https://redirect.example";

        realm.Options.Cors.Enabled = true;
        await storage.Realms.SaveAsync(realm);
        AddClient(memoryStorage, realm, clientId, secret, redirectUri: $"{origin}/callback");

        var response = await SendTokenRequestAsync(realm, clientId, "wrong-secret", origin);

        Assert.Null(GetHeader(response, AccessControlAllowOrigin));
    }

    [Fact]
    public async Task Cors_ActualCacheableRequest_WhenOriginDenied_ReturnsVaryOriginWithoutCorsHeaders()
    {
        var realm = await CreateRealmAsync("cors-cacheable-denied");
        var storage = factory.Services.GetRequiredService<IStorage>();
        const string origin = "https://blocked.example";

        realm.Options.Cors.Enabled = true;
        await storage.Realms.SaveAsync(realm);

        var response = await SendGetAsync(Oidc.Routes.BuildDiscoveryConfigurationUrl(realm.Path), origin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(GetHeader(response, AccessControlAllowOrigin));
        Assert.Contains(Origin, GetHeader(response, Vary)!.Split(',', StringSplitOptions.TrimEntries));
    }

    [Fact]
    public void Cors_ClientAllowedOrigins_UsesCaseInsensitiveComparer()
    {
        var client = new Client();

        client.AllowedCorsOrigins.Add("https://client.example");

        Assert.Same(StringComparer.OrdinalIgnoreCase, client.AllowedCorsOrigins.Comparer);
        Assert.False(client.AllowedCorsOrigins.Add("https://CLIENT.example"));
        Assert.Single(client.AllowedCorsOrigins);
    }

    [Fact]
    public async Task Cors_Preflight_WithoutClientId_DoesNotUseClientOrigins()
    {
        var realm = await CreateRealmAsync("cors-no-client");
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var clientId = $"cors-no-client-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();
        const string origin = "https://client-only.example";

        realm.Options.Cors.Enabled = true;
        await storage.Realms.SaveAsync(realm);
        AddClient(memoryStorage, realm, clientId, secret, allowedCorsOrigin: origin);

        var response = await SendPreflightAsync(
            Oidc.Routes.BuildTokenUrl(realm.Path),
            origin,
            HttpMethods.Post,
            "content-type");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(GetHeader(response, AccessControlAllowOrigin));
    }

    [Fact]
    public async Task Cors_Preflight_WithClientIdQuery_WhenOriginAllowedByClient_ReturnsCorsHeaders()
    {
        var realm = await CreateRealmAsync("cors-client-preflight");
        var storage = factory.Services.GetRequiredService<IStorage>();
        var memoryStorage = factory.Services.GetRequiredService<MemoryStorage>();
        var clientId = $"cors-client-preflight-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();
        const string origin = "https://client-preflight.example";

        realm.Options.Cors.Enabled = true;
        await storage.Realms.SaveAsync(realm);
        AddClient(memoryStorage, realm, clientId, secret, allowedCorsOrigin: origin);

        var response = await SendPreflightAsync(
            $"{Oidc.Routes.BuildTokenUrl(realm.Path)}?client_id={Uri.EscapeDataString(clientId)}",
            origin,
            HttpMethods.Post,
            "content-type");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(origin, GetHeader(response, AccessControlAllowOrigin));
    }

    [Fact]
    public async Task Cors_Preflight_WhenRequestedHeaderIsNotAllowed_IsRejected()
    {
        var realm = await CreateRealmAsync("cors-header");
        var storage = factory.Services.GetRequiredService<IStorage>();
        const string origin = "https://app.example";

        realm.Options.Cors.Enabled = true;
        realm.Options.Cors.AllowedOrigins.Add(origin);
        await storage.Realms.SaveAsync(realm);

        var response = await SendPreflightAsync(
            Oidc.Routes.BuildTokenUrl(realm.Path),
            origin,
            HttpMethods.Post,
            "x-extra-header");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(GetHeader(response, AccessControlAllowOrigin));
    }

    [Fact]
    public async Task Cors_Preflight_WhenWildcardOriginAndCredentialsEnabled_IsRejected()
    {
        var realm = await CreateRealmAsync("cors-wildcard");
        var storage = factory.Services.GetRequiredService<IStorage>();
        const string origin = "https://app.example";

        realm.Options.Cors.Enabled = true;
        realm.Options.Cors.AllowCredentials = true;
        realm.Options.Cors.AllowedOrigins.Add("*");
        await storage.Realms.SaveAsync(realm);

        var response = await SendPreflightAsync(
            Oidc.Routes.BuildTokenUrl(realm.Path),
            origin,
            HttpMethods.Post,
            "content-type");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(GetHeader(response, AccessControlAllowOrigin));
    }

    [Fact]
    public void RealmOptions_CopyFromServer_PropagatesPhase5CorsValues()
    {
        var serverOptions = new ServerOptions
        {
            Cors =
            {
                Enabled = true,
                AllowCredentials = true
            }
        };
        serverOptions.Cors.AllowedOrigins.Add("https://server.example");
        serverOptions.Cors.AllowedHeaders.Add("x-server-header");
        serverOptions.Cors.AllowedMethods.Add("PATCH");

        var realmOptions = new RealmOptions(serverOptions);

        Assert.True(realmOptions.Cors.Enabled);
        Assert.True(realmOptions.Cors.AllowCredentials);
        Assert.Contains("https://server.example", realmOptions.Cors.AllowedOrigins);
        Assert.Contains("x-server-header", realmOptions.Cors.AllowedHeaders);
        Assert.Contains("PATCH", realmOptions.Cors.AllowedMethods);
        Assert.NotSame(serverOptions.Cors, realmOptions.Cors);
        Assert.NotSame(serverOptions.Cors.AllowedOrigins, realmOptions.Cors.AllowedOrigins);
        Assert.NotSame(serverOptions.Cors.AllowedHeaders, realmOptions.Cors.AllowedHeaders);
        Assert.NotSame(serverOptions.Cors.AllowedMethods, realmOptions.Cors.AllowedMethods);
    }

    private async Task<RoyalIdentity.Models.Realm> CreateRealmAsync(string prefix)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var path = $"{prefix}-{suffix}";

        return await manager.CreateAsync(path, $"{path}.test", $"Test Realm {suffix}");
    }

    private async Task<HttpResponseMessage> SendPreflightAsync(
        string url,
        string origin,
        string requestedMethod,
        string? requestedHeaders = null)
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, url);

        request.Headers.TryAddWithoutValidation(Origin, origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", requestedMethod);

        if (requestedHeaders.IsPresent())
        {
            request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", requestedHeaders);
        }

        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendGetAsync(string url, string origin)
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.TryAddWithoutValidation(Origin, origin);

        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendTokenRequestAsync(
        RoyalIdentity.Models.Realm realm,
        string clientId,
        string secret,
        string origin)
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, Oidc.Routes.BuildTokenUrl(realm.Path))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Oidc.Token.Request.GrantType] = "client_credentials",
                [Oidc.Token.Request.ClientId] = clientId,
                [Oidc.Token.Request.ClientSecret] = secret,
                [Oidc.Token.Request.Scope] = "api"
            })
        };
        request.Headers.TryAddWithoutValidation(Origin, origin);

        return await client.SendAsync(request);
    }

    private static void AddClient(
        MemoryStorage storage,
        RoyalIdentity.Models.Realm realm,
        string clientId,
        string secret,
        string? allowedCorsOrigin = null,
        string? redirectUri = null)
    {
        var client = new Client
        {
            Realm = realm,
            Id = clientId,
            Name = $"CORS Client {clientId}",
            RequireClientSecret = true,
            AllowedGrantTypes = ["client_credentials"],
            AllowedScopes = { "api" },
            AllowedResponseTypes = { "code" },
            ClientSecrets = { new ClientSecret(secret.Sha512()) }
        };

        if (allowedCorsOrigin.IsPresent())
        {
            client.AllowedCorsOrigins.Add(allowedCorsOrigin);
        }

        if (redirectUri.IsPresent())
        {
            client.RedirectUris.Add(redirectUri);
        }

        storage.GetRealmMemoryStore(realm).Clients[clientId] = client;
    }

    private static string? GetHeader(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values)
            ? values.Single()
            : null;
    }
}
