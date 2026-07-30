using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoyalIdentity.Models;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// The end-to-end acceptance of plan-replay-protection Fase 3: the same client assertion presented twice to the
/// real token endpoint, against the <b>durable</b> backing on a real PostgreSQL — accepted, then refused.
/// <para>
/// Everything below it is already covered elsewhere: the store's atomicity by the Operational concurrency
/// suites, the protocol path by the SQLite integration scenarios. What only this can answer is whether the
/// production shape — evaluator, Operational EF store, PostgreSQL uniqueness — refuses a replay when wired
/// together.
/// </para>
/// </summary>
public class PostgreSqlPrivateKeyJwtReplayTests
    : IClassFixture<PostgreSqlReplayProtectionAppFactory>, IDisposable
{
    private readonly PostgreSqlReplayProtectionAppFactory factory;
    private readonly PrivateKeyJwtTestKey key = new();
    private bool disposed;

    public PostgreSqlPrivateKeyJwtReplayTests(PostgreSqlReplayProtectionAppFactory factory)
    {
        this.factory = factory;
    }

    [PostgreSqlIntegrationFact]
    [Trait("Category", "PostgreSql")]
    public async Task TheSameAssertionPresentedTwice_IsAcceptedThenRefused()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync("pkj_pg_replay_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http);
        var assertion = CreateAssertion(clientId, tokenEndpoint, "pkj-pg-replay-jti");
        var countBefore = await factory.CountReplayHandlesAsync();

        var first = await PresentAsync(http, tokenEndpoint, assertion);
        var second = await PresentAsync(http, tokenEndpoint, assertion);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        Assert.NotEqual(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains(
            "invalid_client", await second.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // The refusal came from a row that is actually there — not from the request failing for some other
        // reason before the store was ever reached. The delta makes this independent of fixture test order.
        Assert.Equal(countBefore + 1, await factory.CountReplayHandlesAsync());
    }

    /// <summary>
    /// And the refusal is about the identifier, not about the client: a fresh <c>jti</c> from the same client
    /// still authenticates. Without this, a backing that simply refused everything after the first call would
    /// satisfy the scenario above.
    /// </summary>
    [PostgreSqlIntegrationFact]
    [Trait("Category", "PostgreSql")]
    public async Task AFreshIdentifierFromTheSameClient_IsStillAccepted()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync("pkj_pg_fresh_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http);

        var first = await PresentAsync(http, tokenEndpoint,
            CreateAssertion(clientId, tokenEndpoint, "pkj-pg-fresh-jti-1"));
        var second = await PresentAsync(http, tokenEndpoint,
            CreateAssertion(clientId, tokenEndpoint, "pkj-pg-fresh-jti-2"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    private Task<string> SaveClientAsync(string clientId)
        => factory.SaveClientAsync(factory.Handles.Demo, clientId, configured =>
        {
            configured.Name = clientId;
            configured.ClientType = ClientType.Confidential;
            configured.RequireClientSecret = true;
            configured.AllowAllResourceServers = true;
            configured.AllowedGrantTypes.Clear();
            configured.AllowedGrantTypes.Add("client_credentials");
            configured.Secrets.Add(key.CreateClientSecret());
        }).ContinueWith(_ => clientId, TaskContinuationOptions.OnlyOnRanToCompletion);

    private string CreateAssertion(string clientId, string tokenEndpoint, string jti)
    {
        var now = DateTimeOffset.UtcNow;
        return key.CreateAssertion(clientId, tokenEndpoint, jti, now, now + TimeSpan.FromMinutes(5));
    }

    private async Task<string> GetTokenEndpointAsync(HttpClient http)
    {
        var discovery = await http.GetFromJsonAsync<Dictionary<string, JsonElement>>(
            $"/{factory.Handles.Demo.Path}/.well-known/openid-configuration");

        return discovery![Oidc.Discovery.TokenEndpoint].GetString()!;
    }

    private static Task<HttpResponseMessage> PresentAsync(
        HttpClient http, string tokenEndpoint, string assertion)
        => http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = Oidc.ClientAssertionTypes.JwtBearer,
            ["client_assertion"] = assertion,
            ["scope"] = "api",
        }));

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        key.Dispose();
        GC.SuppressFinalize(this);
    }
}
