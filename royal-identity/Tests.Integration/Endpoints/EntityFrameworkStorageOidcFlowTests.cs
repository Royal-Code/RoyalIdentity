using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Data.Operational.Entities;
using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.EntityFramework.Sqlite;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// One complete OIDC flow over the <b>EF gateway</b> — authorize, login, code exchange, userinfo and refresh —
/// with Configuration and Operational persisted in one SQLite database migrated and seeded by the production
/// runner (plan Fase 8). It is the end-to-end answer to "is this backing actually usable?", which no store
/// contract can give on its own.
/// <para>
/// Opt-in by construction: it uses <see cref="EntityFrameworkStorageAppFactory"/> while every other suite keeps
/// the in-memory backing, so nothing here changes the default the host or the other tests run on (ADR-018).
/// </para>
/// </summary>
public class EntityFrameworkStorageOidcFlowTests : IClassFixture<EntityFrameworkStorageAppFactory>
{
    private readonly EntityFrameworkStorageAppFactory factory;

    public EntityFrameworkStorageOidcFlowTests(EntityFrameworkStorageAppFactory factory)
        => this.factory = factory;

    [Fact]
    public async Task AuthorizationCodeFlow_OverTheEfGateway_IssuesTokensAndPersistsTheOperationalState()
    {
        var client = factory.CreateClient();

        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeChallenge = Base64Url.Encode(Encoding.ASCII.GetBytes(codeVerifier).Sha256());
        var redirectUri = $"{client.BaseAddress}callback";

        var authorizeUrl = Oidc.Routes.BuildAuthorizeUrl("demo")
            .AddQueryString("client_id", "demo_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile email offline_access")
            .AddQueryString("redirect_uri", redirectUri)
            .AddQueryString("state", "ef-state")
            .AddQueryString("code_challenge", codeChallenge)
            .AddQueryString("code_challenge_method", "S256");

        // 1 — authorize redirects to the login page, and the authorize parameters are already persisted.
        var loginPage = await client.GetAsync(authorizeUrl);
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        Assert.Equal(1, await CountAsync<AuthorizeParametersEntity>());

        var document = new HtmlDocument();
        document.LoadHtml(await loginPage.Content.ReadAsStringAsync());
        var callback = await new FormAction(client, document.DocumentNode.SelectSingleNode("//form"))
            .SetValue("Input.Username", "alice")
            .SetValue("Input.Password", "alice")
            .SubmitAsync();

        // 2 — the login lands on the client callback carrying the authorization code.
        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        var callbackData = JsonSerializer.Deserialize<Dictionary<string, string>>(
            await callback.Content.ReadAsStringAsync());
        Assert.NotNull(callbackData);
        Assert.Equal("ef-state", callbackData["state"]);

        // The session and the code are rows now; the authorize parameters were consumed by the callback.
        Assert.Equal(1, await CountAsync<UserSessionEntity>());
        Assert.Equal(1, await CountArtifactsAsync(ProtocolArtifactTypes.AuthorizationCode));
        Assert.Equal(0, await CountAsync<AuthorizeParametersEntity>());

        // 3 — the code is exchanged for tokens.
        var tokens = await ExchangeAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = callbackData["code"],
            ["client_id"] = "demo_client",
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
        });

        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrEmpty(tokens.GetProperty("id_token").GetString()));
        var refreshToken = tokens.GetProperty("refresh_token").GetString()!;

        // MP-2: consuming the code removed it in the same operation, and the refresh token was persisted.
        Assert.Equal(0, await CountArtifactsAsync(ProtocolArtifactTypes.AuthorizationCode));
        Assert.Equal(1, await CountArtifactsAsync(ProtocolArtifactTypes.RefreshToken));

        // 4 — the access token works against a protected endpoint.
        var userInfo = await GetUserInfoAsync(client, tokens.GetProperty("access_token").GetString()!);
        Assert.Equal(MemoryStorage.AliceSubjectId, userInfo.GetProperty("sub").GetString());

        // 5 — MP-3 through the flow: the refresh renews the access token, and the conditional transition marked
        // the very same row as consumed instead of writing a second one.
        var refreshed = await ExchangeAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = "demo_client",
        });

        Assert.False(string.IsNullOrEmpty(refreshed.GetProperty("access_token").GetString()));
        Assert.NotEqual(
            tokens.GetProperty("access_token").GetString(),
            refreshed.GetProperty("access_token").GetString());

        // The rotation persisted a second handle and marked the first as consumed — the conditional transition
        // wrote a state version, which is what MP-3 rests on.
        Assert.Equal(2, await CountArtifactsAsync(ProtocolArtifactTypes.RefreshToken));
        var consumed = Assert.Single(await ConsumedRefreshTokensAsync());
        Assert.True(consumed.StateVersion > 0);

        // The demo client keeps the product default tolerance (unrestricted reuse), so replaying the original
        // handle is accepted — and it rotates again rather than duplicating the row it came from.
        await ExchangeAsync(client, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = "demo_client",
        });

        Assert.Equal(3, await CountArtifactsAsync(ProtocolArtifactTypes.RefreshToken));
        Assert.Single(await ConsumedRefreshTokensAsync());
    }

    // The whole point of the fixture: the flow above ran against EF, not the in-memory fake.
    [Fact]
    public void TheHost_ResolvesTheEntityFrameworkGateway()
    {
        using var scope = factory.Services.CreateScope();

        var storage = scope.ServiceProvider.GetRequiredService<RoyalIdentity.Contracts.Storage.IStorage>();

        Assert.StartsWith(
            "RoyalIdentity.Storage.EntityFramework",
            storage.GetType().FullName,
            StringComparison.Ordinal);
        Assert.IsNotType<MemoryStorage>(storage);
    }

    // DF23: the host never migrates. The runner did, and both families recorded themselves separately.
    [Fact]
    public async Task BothFamilies_WereMigratedByTheRunner_IntoSeparateHistories()
    {
        using var scope = factory.Services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<ConfigurationSqliteDbContext>();
        var operational = scope.ServiceProvider.GetRequiredService<OperationalSqliteDbContext>();

        Assert.Empty(await configuration.Database.GetPendingMigrationsAsync());
        Assert.Empty(await operational.Database.GetPendingMigrationsAsync());
        Assert.Contains(
            await configuration.Database.GetAppliedMigrationsAsync(),
            id => id.EndsWith("_InitialConfiguration", StringComparison.Ordinal));
        Assert.All(
            await operational.Database.GetAppliedMigrationsAsync(),
            id => Assert.EndsWith("_InitialOperational", id, StringComparison.Ordinal));
    }

    private static async Task<HttpResponseMessage> PostTokenAsync(
        HttpClient client, Dictionary<string, string> form)
        => await client.PostAsync(Oidc.Routes.BuildTokenUrl("demo"), new FormUrlEncodedContent(form));

    private static async Task<JsonElement> ExchangeAsync(HttpClient client, Dictionary<string, string> form)
    {
        var response = await PostTokenAsync(client, form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"token endpoint failed: {response.StatusCode} {body}");

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<JsonElement> GetUserInfoAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Oidc.Routes.BuildUserInfoUrl("demo"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"userinfo failed: {response.StatusCode} {body}");

        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<int> CountAsync<TEntity>() where TEntity : class
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OperationalSqliteDbContext>();

        return await context.Set<TEntity>().AsNoTracking().CountAsync();
    }

    private async Task<List<ProtocolArtifactEntity>> ConsumedRefreshTokensAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OperationalSqliteDbContext>();

        return await context.Set<ProtocolArtifactEntity>()
            .AsNoTracking()
            .Where(artifact =>
                artifact.ArtifactType == ProtocolArtifactTypes.RefreshToken && artifact.ConsumedAtUtc != null)
            .ToListAsync();
    }

    private async Task<int> CountArtifactsAsync(string artifactType)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OperationalSqliteDbContext>();

        return await context.Set<ProtocolArtifactEntity>()
            .AsNoTracking()
            .CountAsync(artifact => artifact.ArtifactType == artifactType);
    }
}
