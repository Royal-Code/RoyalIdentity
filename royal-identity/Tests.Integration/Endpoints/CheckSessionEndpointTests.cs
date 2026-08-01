using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Fase 4 of plan-oidc-session-management: the check-session route, discovery promise, effective HTTPS origin
/// and realm options form one observable contract.
/// </summary>
public class CheckSessionEndpointTests
    : IClassFixture<CheckSessionEndpointTests.ForwardedHeadersAppFactory>,
      IClassFixture<CheckSessionEndpointTests.UntrustedProxyAppFactory>
{
    private readonly ForwardedHeadersAppFactory factory;
    private readonly UntrustedProxyAppFactory untrustedProxyFactory;

    public CheckSessionEndpointTests(
        ForwardedHeadersAppFactory factory,
        UntrustedProxyAppFactory untrustedProxyFactory)
    {
        this.factory = factory;
        this.untrustedProxyFactory = untrustedProxyFactory;
    }

    [Fact]
    public async Task HttpsEnabled_GetIsHtmlAndUnsupportedMethodIs405()
    {
        var client = CreateHttpsClient();
        var url = Oidc.Routes.BuildCheckSessionUrl(factory.Handles.Demo.Path);

        var get = await client.GetAsync(url);
        var post = await client.PostAsync(url, new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal("text/html", get.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            $"{Server.DefaultCheckSessionCookieName}.{factory.Handles.Demo.Path}",
            await get.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Equal("GET", string.Join(", ", post.Content.Headers.Allow));
        Assert.Equal("application/problem+json", post.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HttpsIframe_UsesFreshNonceStrictHeadersAndRemainsFrameable()
    {
        var client = CreateHttpsClient();
        var url = Oidc.Routes.BuildCheckSessionUrl(factory.Handles.Demo.Path);

        var first = await client.GetAsync(url);
        var second = await client.GetAsync(url);
        var firstHtml = await first.Content.ReadAsStringAsync();
        var secondHtml = await second.Content.ReadAsStringAsync();
        var firstNonce = ExtractNonce(firstHtml);
        var secondNonce = ExtractNonce(secondHtml);
        var csp = Assert.Single(first.Headers.GetValues("Content-Security-Policy"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("text/html", first.Content.Headers.ContentType?.MediaType);
        Assert.Equal("UTF-8", first.Content.Headers.ContentType?.CharSet);
        Assert.Equal("no-referrer", Assert.Single(first.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("nosniff", Assert.Single(first.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("no-store", Assert.Single(first.Headers.GetValues("Cache-Control")), StringComparison.Ordinal);
        Assert.Contains("no-cache", Assert.Single(first.Headers.GetValues("Cache-Control")), StringComparison.Ordinal);
        Assert.Equal("no-cache", Assert.Single(first.Headers.GetValues("Pragma")));
        Assert.Equal($"default-src 'none'; script-src 'nonce-{firstNonce}'", csp);
        Assert.DoesNotContain("frame-ancestors", csp, StringComparison.OrdinalIgnoreCase);
        Assert.False(first.Headers.Contains("X-Frame-Options"));
        Assert.Matches("^[A-Za-z0-9_-]{43}$", firstNonce);
        Assert.NotEqual(firstNonce, secondNonce);
        Assert.Single(Regex.Matches(firstHtml, "<script(?:\\s|>)", RegexOptions.CultureInvariant).Cast<Match>());
        Assert.Contains("crypto.subtle.digest('SHA-256', canonical)", firstHtml, StringComparison.Ordinal);
        Assert.Contains("window.parent === window", firstHtml, StringComparison.Ordinal);
        Assert.Contains("event.source !== window.parent", firstHtml, StringComparison.Ordinal);
        Assert.Contains("event.source.postMessage(result, event.origin)", firstHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("postMessage(result, '*')", firstHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("frame-ancestors 'none'", firstHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Http_EndpointIs404ForEveryMethodAndDiscoveryDoesNotAdvertiseIt()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var url = Oidc.Routes.BuildCheckSessionUrl(factory.Handles.Demo.Path);

        var get = await client.GetAsync(url);
        var post = await client.PostAsync(url, new StringContent(string.Empty));
        var discovery = await GetDiscoveryAsync(client, factory.Handles.Demo.Path);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.False(discovery.TryGetProperty(Oidc.Discovery.CheckSessionIframe, out _));
    }

    [Fact]
    public async Task Disabled_HttpsEndpointIs404ForEveryMethodAndDiscoveryDoesNotAdvertiseIt()
    {
        await factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.Endpoints.EnableCheckSessionEndpoint = false);
        try
        {
            var client = CreateHttpsClient();
            var url = Oidc.Routes.BuildCheckSessionUrl(factory.Handles.Demo.Path);

            var get = await client.GetAsync(url);
            var post = await client.PostAsync(url, new StringContent(string.Empty));
            var discovery = await GetDiscoveryAsync(client, factory.Handles.Demo.Path);

            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
            Assert.False(discovery.TryGetProperty(Oidc.Discovery.CheckSessionIframe, out _));
        }
        finally
        {
            await factory.UpdateRealmAsync(
                factory.Handles.Demo,
                options => options.Endpoints.EnableCheckSessionEndpoint = true);
        }
    }

    [Fact]
    public async Task HttpsDiscovery_PublishesExternalHostPortRealmAndALiveEndpoint()
    {
        var client = CreateHttpsClient("https://public-idp.example:8443");

        var discovery = await GetDiscoveryAsync(client, factory.Handles.Demo.Path);
        var advertised = new Uri(discovery.GetProperty(Oidc.Discovery.CheckSessionIframe).GetString()!);
        var endpoint = await client.GetAsync(advertised.PathAndQuery);

        Assert.Equal("https", advertised.Scheme);
        Assert.Equal("public-idp.example", advertised.Host);
        Assert.Equal(8443, advertised.Port);
        Assert.Equal($"/{factory.Handles.Demo.Path}/connect/checksession", advertised.AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, endpoint.StatusCode);
    }

    [Fact]
    public async Task ForwardedSchemeAndHost_DetermineDiscoveryAndEndpointAvailability()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Host", "edge-idp.example:9443");

        var discovery = await GetDiscoveryAsync(client, factory.Handles.Demo.Path);
        var advertised = discovery.GetProperty(Oidc.Discovery.CheckSessionIframe).GetString();
        var endpoint = await client.GetAsync(Oidc.Routes.BuildCheckSessionUrl(factory.Handles.Demo.Path));

        Assert.Equal(
            $"https://edge-idp.example:9443/{factory.Handles.Demo.Path}/connect/checksession",
            advertised);
        Assert.Equal(HttpStatusCode.OK, endpoint.StatusCode);
    }

    [Fact]
    public async Task ForwardedHeaders_FromUntrustedPeerCannotUnlockEndpointOrDiscovery()
    {
        var client = untrustedProxyFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Host", "attacker.example");
        var realm = untrustedProxyFactory.Handles.Demo.Path;

        var endpoint = await client.GetAsync(Oidc.Routes.BuildCheckSessionUrl(realm));
        var discovery = await GetDiscoveryAsync(client, realm);

        Assert.Equal(HttpStatusCode.NotFound, endpoint.StatusCode);
        Assert.False(discovery.TryGetProperty(Oidc.Discovery.CheckSessionIframe, out _));
    }

    [Fact]
    public async Task TwoRealms_KeepGateCookieHtmlAndCspIsolated()
    {
        var suffix = CryptoRandom.CreateUniqueId(5, OutputFormat.Hex);
        var realmA = await CreateRealmAsync($"check-a-{suffix}", addDeprecatedCspHeader: false);
        var realmB = await CreateRealmAsync($"check-b-{suffix}", addDeprecatedCspHeader: true);
        var client = CreateHttpsClient();

        var responseA = await client.GetAsync(Oidc.Routes.BuildCheckSessionUrl(realmA.Path));
        var responseB = await client.GetAsync(Oidc.Routes.BuildCheckSessionUrl(realmB.Path));
        var htmlA = await responseA.Content.ReadAsStringAsync();
        var htmlB = await responseB.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
        Assert.Contains($"check-a-{suffix}.{realmA.Path}", htmlA, StringComparison.Ordinal);
        Assert.DoesNotContain($"check-b-{suffix}.{realmB.Path}", htmlA, StringComparison.Ordinal);
        Assert.Contains($"check-b-{suffix}.{realmB.Path}", htmlB, StringComparison.Ordinal);
        Assert.DoesNotContain($"check-a-{suffix}.{realmA.Path}", htmlB, StringComparison.Ordinal);
        Assert.True(responseA.Headers.Contains("Content-Security-Policy"));
        Assert.False(responseA.Headers.Contains("X-Content-Security-Policy"));
        Assert.True(responseB.Headers.Contains("Content-Security-Policy"));
        Assert.True(responseB.Headers.Contains("X-Content-Security-Policy"));

        realmB.Options.Endpoints.EnableCheckSessionEndpoint = false;
        await factory.SaveRealmAsync(realmB);

        var stillAvailableA = await client.GetAsync(Oidc.Routes.BuildCheckSessionUrl(realmA.Path));
        var unavailableB = await client.GetAsync(Oidc.Routes.BuildCheckSessionUrl(realmB.Path));
        var discoveryA = await GetDiscoveryAsync(client, realmA.Path);
        var discoveryB = await GetDiscoveryAsync(client, realmB.Path);

        Assert.Equal(HttpStatusCode.OK, stillAvailableA.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unavailableB.StatusCode);
        Assert.True(discoveryA.TryGetProperty(Oidc.Discovery.CheckSessionIframe, out _));
        Assert.False(discoveryB.TryGetProperty(Oidc.Discovery.CheckSessionIframe, out _));
    }

    private HttpClient CreateHttpsClient(string origin = "https://localhost")
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri(origin),
        });

    private async Task<RoyalIdentity.Models.Realm> CreateRealmAsync(
        string checkSessionCookieName,
        bool addDeprecatedCspHeader)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(5, OutputFormat.Hex);
        var path = $"check-{suffix}";
        var realm = await manager.CreateAsync(path, $"{path}.test", $"Check Session {suffix}");
        realm.Options.Authentication.CheckSessionCookieName = checkSessionCookieName;
        realm.Options.Endpoints.EnableCheckSessionEndpoint = true;
        realm.Options.Csp.AddDeprecatedHeader = addDeprecatedCspHeader;
        factory.Resources.SetResourceServer(
            realm.Id,
            TestConfigurationResourceSource.CreateDemoResourceServer());
        await factory.SaveRealmAsync(realm);
        return realm;
    }

    private static async Task<JsonElement> GetDiscoveryAsync(HttpClient client, string realm)
    {
        var response = await client.GetAsync(Oidc.Routes.BuildDiscoveryConfigurationUrl(realm));
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static string ExtractNonce(string html)
    {
        var match = Regex.Match(
            html,
            "<script nonce=\"(?<nonce>[A-Za-z0-9_-]+)\">",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The iframe must contain one nonce-bearing inline script.");
        return match.Groups["nonce"].Value;
    }

    public sealed class ForwardedHeadersAppFactory : PersistentStorageAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(
                new RemoteIpStartupFilter(IPAddress.Loopback)));
        }
    }

    public sealed class UntrustedProxyAppFactory : PersistentStorageAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(
                new RemoteIpStartupFilter(IPAddress.Parse("203.0.113.99"))));
        }
    }

    private sealed class RemoteIpStartupFilter(IPAddress remoteIpAddress) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = remoteIpAddress;
                    await nextMiddleware();
                });
                next(app);
            };
        }
    }
}
