using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoyalIdentity.Models;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Guards invariant 4 of plan-replay-protection at the seam that was actually changed in Fase 1: the evaluator's
/// <c>try/catch</c> was narrowed to token validation alone, so a failure of the replay-protection backing
/// propagates instead of being reported as a credential problem.
/// <para>
/// Without this test, widening that <c>catch</c> again would silently turn a security control into an
/// <c>invalid_client</c> per request — the exact shape of degradation this plan exists to remove.
/// </para>
/// </summary>
public class PrivateKeyJwtBackingFailureTests
    : IClassFixture<FailingReplayProtectionAppFactory>, IDisposable
{
    private readonly FailingReplayProtectionAppFactory factory;
    private readonly PrivateKeyJwtTestKey key = new();
    private bool disposed;

    public PrivateKeyJwtBackingFailureTests(FailingReplayProtectionAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task BackingFailure_IsNotTranslatedIntoAnAuthenticationOutcome()
    {
        const string clientId = "pkj_backing_failure_client";
        var http = factory.CreateClient();

        await factory.SaveClientAsync(factory.Handles.Demo, clientId, configured =>
        {
            configured.Name = clientId;
            configured.ClientType = ClientType.Confidential;
            configured.RequireClientSecret = true;
            configured.AllowAllResourceServers = true;
            configured.AllowedGrantTypes.Clear();
            configured.AllowedGrantTypes.Add("client_credentials");
            configured.Secrets.Add(key.CreateClientSecret());
        });

        var discovery = await http.GetFromJsonAsync<Dictionary<string, JsonElement>>(
            $"/{factory.Handles.Demo.Path}/.well-known/openid-configuration");
        var tokenEndpoint = discovery![Oidc.Discovery.TokenEndpoint].GetString()!;

        var now = DateTimeOffset.UtcNow;
        var assertion = key.CreateAssertion(
            clientId, tokenEndpoint, "pkj-backing-failure-jti", now, now + TimeSpan.FromMinutes(5));

        var response = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_assertion_type"] = Oidc.ClientAssertionTypes.JwtBearer,
                ["client_assertion"] = assertion,
                ["scope"] = "api",
            }));

        var body = await response.Content.ReadAsStringAsync();

        // Not authorized, and not disguised: the backing is unavailable, so the answer is an outage and not a
        // verdict about the credential. Invariant 10 runs both ways — client data never becomes a 5xx, and
        // infrastructure never becomes an OAuth error — and this is the second direction.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("access_token", body, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid_client", body, StringComparison.Ordinal);
        Assert.DoesNotContain("invalid_grant", body, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        key.Dispose();
        GC.SuppressFinalize(this);
    }
}
