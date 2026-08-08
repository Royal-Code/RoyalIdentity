using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// The issuer the server derives from the request when a realm does not configure one
/// (plan-data-operational-storage Fase 8). Two defects lived here unnoticed because nothing asserted the
/// <b>value</b>: the realm path was appended to the whole URI instead of to its trailing slash, producing
/// <c>http://hosthttp://host/realm</c>; and the result was written back into <c>RealmOptions.IssuerUri</c>, so
/// correctness depended on the backing handing out one shared options instance that an earlier request had
/// populated.
/// </summary>
public class IssuerUriTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public IssuerUriTests(PersistentStorageAppFactory factory) => this.factory = factory;

    [Fact]
    public async Task Discovery_PublishesTheIssuerDerivedFromTheRequest()
    {
        var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>(
            Oidc.Routes.BuildDiscoveryConfigurationUrl(factory.Handles.Demo.Path));

        var issuer = document.GetProperty("issuer").GetString();

        Assert.Equal($"{client.BaseAddress!.ToString().TrimEnd('/')}/{factory.Handles.Demo.Path}", issuer);
    }

    // The derivation itself: one origin, one realm segment, no repetition — and it does not depend on, nor
    // mutate, the realm options it was given.
    [Fact]
    public void DerivedIssuer_AppendsTheRealmPathOnce_AndDoesNotMutateTheOptions()
    {
        var options = new RealmOptions(new ServerOptions());
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("idp.example");
        httpContext.Items[Server.RealmRouteKey] = "tenant";

        var issuer = httpContext.GetServerIssuerUri(options);

        Assert.Equal("https://idp.example/tenant", issuer);
        Assert.DoesNotContain("https://idp.examplehttps", issuer, StringComparison.Ordinal);
        // No write-back: a second call over another host must not return the first one's answer.
        Assert.Null(options.IssuerUri);

        var otherContext = new DefaultHttpContext();
        otherContext.Request.Scheme = "https";
        otherContext.Request.Host = new HostString("other.example");
        otherContext.Items[Server.RealmRouteKey] = "tenant";

        Assert.Equal("https://other.example/tenant", otherContext.GetServerIssuerUri(options));
    }

    // An explicitly configured issuer always wins, whatever host served the request.
    [Fact]
    public void ConfiguredIssuer_WinsOverTheDerivedOne()
    {
        var options = new RealmOptions(new ServerOptions()) { IssuerUri = "https://issuer.configured.test" };
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("idp.example");
        httpContext.Items[Server.RealmRouteKey] = "tenant";

        Assert.Equal("https://issuer.configured.test", httpContext.GetServerIssuerUri(options));
    }

    // A token issued by the server must carry — and be accepted against — the issuer the same server derives.
    // That property was quietly provided by the write-back, and any real storage backing would have broken it.
    [Fact]
    public async Task AnIssuedToken_CarriesTheDerivedIssuer_AndIsAcceptedByAProtectedEndpoint()
    {
        var client = factory.CreateClient();
        var realm = await factory.WithStorageValueAsync(
            storage => storage.Realms.GetByPathAsync(factory.Handles.Demo.Path, default));

        Assert.NotNull(realm);
        // The realm derives its issuer; nothing has pinned it, and nothing below may pin it either.
        Assert.Null(realm.Options.IssuerUri);

        var expectedIssuer = $"{client.BaseAddress!.ToString().TrimEnd('/')}/{factory.Handles.Demo.Path}";
        var accessToken = await IssueAccessTokenAsync(client);

        Assert.Equal(expectedIssuer, IssuerOf(accessToken));

        // Accepted by a protected endpoint, which validates the signature and the issuer.
        var request = new HttpRequestMessage(
            HttpMethod.Get, Oidc.Routes.BuildUserInfoUrl(factory.Handles.Demo.Path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var userInfo = await client.SendAsync(request);

        Assert.True(
            userInfo.IsSuccessStatusCode,
            $"userinfo rejected the token: {userInfo.StatusCode} {await userInfo.Content.ReadAsStringAsync()}");

        // Issuing and validating never pinned the issuer onto the realm options.
        var reloaded = await factory.WithStorageValueAsync(
            storage => storage.Realms.GetByPathAsync(factory.Handles.Demo.Path, default));
        Assert.Null(reloaded!.Options.IssuerUri);
    }

    /// <summary>Runs the authorization-code flow far enough to hold a real access token.</summary>
    private async Task<string> IssueAccessTokenAsync(HttpClient client)
    {
        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeChallenge = Base64Url.Encode(Encoding.ASCII.GetBytes(codeVerifier).Sha256());
        const string redirectUri = "https://localhost/callback";

        var authorizeUrl = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile email")
            .AddQueryString("redirect_uri", redirectUri)
            .AddQueryString("state", "issuer-state")
            .AddQueryString("code_challenge", codeChallenge)
            .AddQueryString("code_challenge_method", "S256");

        var loginPage = await client.GetAsync(authorizeUrl);
        var document = new HtmlDocument();
        document.LoadHtml(await loginPage.Content.ReadAsStringAsync());
        var callback = await new FormAction(client, document.DocumentNode.SelectSingleNode("//form"))
            .SetValue("Input.Username", "alice")
            .SetValue("Input.Password", "alice")
            .SubmitAsync();

        var callbackData = JsonSerializer.Deserialize<Dictionary<string, string>>(
            await callback.Content.ReadAsStringAsync())!;

        var tokenResponse = await client.PostAsync(
            Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = callbackData["code"],
                ["client_id"] = "demo_client",
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
            }));

        var body = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.IsSuccessStatusCode, $"token endpoint failed: {tokenResponse.StatusCode} {body}");

        return JsonDocument.Parse(body).RootElement.GetProperty("access_token").GetString()!;
    }

    private static string? IssuerOf(string jwt)
    {
        var payload = Encoding.UTF8.GetString(Base64Url.Decode(jwt.Split('.')[1]));

        return JsonDocument.Parse(payload).RootElement.GetProperty("iss").GetString();
    }
}
