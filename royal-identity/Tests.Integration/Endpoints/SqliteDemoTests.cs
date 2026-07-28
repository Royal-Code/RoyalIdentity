using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Demo;
using RoyalIdentity.Extensions;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class SqliteDemoTests
{
    [Fact]
    public async Task SqliteDemo_StartsEmpty_SeedsOnlyDemoRealm_AndCompletesOidcFlow()
    {
        var factory = new WebApplicationFactory<DemoProgram>();
        var keyRingPath = string.Empty;

        try
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
            var lifetime = factory.Services.GetRequiredService<DemoStorageLifetime>();
            keyRingPath = lifetime.KeyRingPath;
            Assert.True(Directory.Exists(keyRingPath));

            await AssertOnlyDemoRealmAsync(factory.Services);

            var codeVerifier = CryptoRandom.CreateUniqueId();
            var codeChallenge = Base64Url.Encode(Encoding.ASCII.GetBytes(codeVerifier).Sha256());
            const string redirectUri = "http://localhost/callback";
            var authorizeUrl = Oidc.Routes.BuildAuthorizeUrl(DemoConstants.RealmPath)
                .AddQueryString("client_id", DemoConstants.ClientId)
                .AddQueryString("response_type", "code")
                .AddQueryString("response_mode", "query")
                .AddQueryString("scope", "openid profile email")
                .AddQueryString("redirect_uri", redirectUri)
                .AddQueryString("state", "demo-state")
                .AddQueryString("code_challenge", codeChallenge)
                .AddQueryString("code_challenge_method", "S256");

            var authorize = await client.GetAsync(authorizeUrl);
            Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
            Assert.NotNull(authorize.Headers.Location);

            var loginPage = await client.GetAsync(authorize.Headers.Location);
            Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
            var document = new HtmlDocument();
            document.LoadHtml(await loginPage.Content.ReadAsStringAsync());

            var callback = await new FormAction(client, document.DocumentNode.SelectSingleNode("//form"))
                .SetValue("Input.Username", DemoConstants.Username)
                .SetValue("Input.Password", DemoConstants.Password)
                .SubmitAsync();
            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
            Assert.NotNull(callback.Headers.Location);

            var callbackLocation = new Uri(client.BaseAddress!, callback.Headers.Location);
            for (var redirect = 0; redirect < 3 && callbackLocation.AbsolutePath != "/callback"; redirect++)
            {
                var protocolRedirect = await client.GetAsync(callbackLocation);
                Assert.Equal(HttpStatusCode.Redirect, protocolRedirect.StatusCode);
                Assert.NotNull(protocolRedirect.Headers.Location);
                callbackLocation = new Uri(client.BaseAddress!, protocolRedirect.Headers.Location);
            }

            Assert.Equal("/callback", callbackLocation.AbsolutePath);
            var callbackQuery = HttpUtility.ParseQueryString(callbackLocation.Query);
            Assert.Equal("demo-state", callbackQuery["state"]);
            var code = callbackQuery["code"];
            Assert.False(string.IsNullOrWhiteSpace(code));

            var tokenResponse = await client.PostAsync(
                Oidc.Routes.BuildTokenUrl(DemoConstants.RealmPath),
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["client_id"] = DemoConstants.ClientId,
                    ["redirect_uri"] = redirectUri,
                    ["code_verifier"] = codeVerifier,
                }));
            var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
            Assert.True(
                tokenResponse.IsSuccessStatusCode,
                $"token endpoint failed: {tokenResponse.StatusCode} {tokenBody}");

            using var tokens = JsonDocument.Parse(tokenBody);
            Assert.False(string.IsNullOrWhiteSpace(tokens.RootElement.GetProperty("access_token").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(tokens.RootElement.GetProperty("id_token").GetString()));
        }
        finally
        {
            await factory.DisposeAsync();
        }

        await WaitForDirectoryRemovalAsync(keyRingPath);
    }

    private static async Task AssertOnlyDemoRealmAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        List<string> realmIds = [];

        await foreach (var realm in storage.Realms.GetAllAsync(CancellationToken.None))
            realmIds.Add(realm.Id);

        Assert.Equal([DemoConstants.RealmId], realmIds);
    }

    private static async Task WaitForDirectoryRemovalAsync(string path)
    {
        var timeout = Stopwatch.StartNew();
        while (Directory.Exists(path) && timeout.Elapsed < TimeSpan.FromSeconds(2))
            await Task.Delay(10);

        Assert.False(Directory.Exists(path));
    }
}
