using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RoyalIdentity.Models;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// End-to-end coverage of <c>private_key_jwt</c> replay protection over the real token endpoint
/// (plan-replay-protection Fase 1). Before this plan the flow had no test at all: the default backing answered
/// "never seen" for every handle, so nothing would have failed if the protection were deleted.
/// </summary>
public class PrivateKeyJwtReplayProtectionTests : IClassFixture<LogCapturingAppFactory>, IDisposable
{
    private readonly LogCapturingAppFactory factory;
    private readonly PrivateKeyJwtTestKey key = new();
    private bool disposed;

    public PrivateKeyJwtReplayProtectionTests(LogCapturingAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task FirstPresentation_IsAccepted_AndTheSamePresentationAgainIsRefused()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync(factory.Handles.Demo, "pkj_replay_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);
        var assertion = CreateAssertion(clientId, tokenEndpoint, "pkj-replay-jti");

        var first = await PresentAsync(http, tokenEndpoint, assertion);
        var second = await PresentAsync(http, tokenEndpoint, assertion);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertRefusedAsCredentialAsync(second);
    }

    // A fresh assertion carrying an already-used jti is the interesting case: the credential is otherwise valid,
    // so only the replay record can refuse it.
    [Fact]
    public async Task ReusedIdentifierInANewAssertion_IsRefused()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync(factory.Handles.Demo, "pkj_reused_jti_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);

        var first = await PresentAsync(http, tokenEndpoint,
            CreateAssertion(clientId, tokenEndpoint, "pkj-reused-jti"));
        var second = await PresentAsync(http, tokenEndpoint,
            CreateAssertion(clientId, tokenEndpoint, "pkj-reused-jti"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertRefusedAsCredentialAsync(second);
    }

    // DF13, issuer dimension: the record is keyed by the validated client_id, so one client cannot burn another
    // client's identifier — which a global key would have allowed, as a denial of service and a weak oracle.
    [Fact]
    public async Task SameIdentifierFromTwoClientsOfTheSameRealm_DoesNotInterfere()
    {
        var http = factory.CreateClient();
        var firstClient = await SaveClientAsync(factory.Handles.Demo, "pkj_issuer_a");
        var secondClient = await SaveClientAsync(factory.Handles.Demo, "pkj_issuer_b");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);

        var first = await PresentAsync(http, tokenEndpoint,
            CreateAssertion(firstClient, tokenEndpoint, "pkj-shared-jti"));
        var second = await PresentAsync(http, tokenEndpoint,
            CreateAssertion(secondClient, tokenEndpoint, "pkj-shared-jti"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    // DF13, realm dimension.
    [Fact]
    public async Task SameIdentifierInTwoRealms_DoesNotInterfere()
    {
        var http = factory.CreateClient();
        const string clientId = "pkj_cross_realm_client";

        factory.Resources.SetResourceServer(
            factory.Handles.Account.Id, TestConfigurationResourceSource.CreateDemoResourceServer());

        await SaveClientAsync(factory.Handles.Demo, clientId);
        await SaveClientAsync(factory.Handles.Account, clientId);

        var demoEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);
        var accountEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Account);

        var inDemo = await PresentAsync(http, demoEndpoint,
            CreateAssertion(clientId, demoEndpoint, "pkj-cross-realm-jti"));
        var inAccount = await PresentAsync(http, accountEndpoint,
            CreateAssertion(clientId, accountEndpoint, "pkj-cross-realm-jti"));

        Assert.Equal(HttpStatusCode.OK, inDemo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inAccount.StatusCode);
    }

    // DF19/DF21: an assertion above the realm ceiling is refused, and — this is the part that matters — it must
    // not register its handle, or a rejected request would poison the identifier for the client's next attempt.
    [Fact]
    public async Task AssertionBeyondTheRealmCeiling_IsRefused_AndRegistersNoHandle()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync(factory.Handles.Demo, "pkj_ceiling_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);
        const string jti = "pkj-ceiling-jti";

        var tooLong = await PresentAsync(http, tokenEndpoint,
            CreateAssertion(clientId, tokenEndpoint, jti, TimeSpan.FromMinutes(20)));
        var withinCeiling = await PresentAsync(http, tokenEndpoint,
            CreateAssertion(clientId, tokenEndpoint, jti, TimeSpan.FromMinutes(5)));

        await AssertRefusedAsCredentialAsync(tooLong);
        Assert.Equal(HttpStatusCode.OK, withinCeiling.StatusCode);
    }

    // The ten-minute default exists precisely so this case keeps working. The client's clock is five minutes
    // ahead, so it stamps nbf five minutes into the server's future and exp five minutes after that: the
    // assertion is five minutes long from the client's point of view and ten minutes ahead from the server's.
    // This is the only case that exercises an `nbf` inside the tolerated skew through the real flow.
    [Fact]
    public async Task AssertionOfFiveMinutesFromAClockFiveMinutesAhead_IsStillAccepted()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync(factory.Handles.Demo, "pkj_skewed_clock_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);

        var clientNow = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5);
        var assertion = key.CreateAssertion(
            clientId, tokenEndpoint, "pkj-skewed-jti", clientNow, clientNow + TimeSpan.FromMinutes(5));

        var response = await PresentAsync(http, tokenEndpoint, assertion);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The default lifetime validation refuses a window that describes nothing, and the injected-clock delegate
    // that replaced it has to refuse the same. Each bound passes its own skew comparison here — the assertion
    // expires now and claims to start four minutes from now — so only the coherence check can catch it.
    [Fact]
    public async Task AssertionStartingAfterItExpires_IsRefused()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync(factory.Handles.Demo, "pkj_incoherent_window_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);

        var now = DateTimeOffset.UtcNow;
        var assertion = key.CreateAssertionWithRawLifetime(
            clientId,
            tokenEndpoint,
            "pkj-incoherent-window-jti",
            notBefore: now + TimeSpan.FromMinutes(4),
            expires: now);

        await AssertRefusedAsCredentialAsync(await PresentAsync(http, tokenEndpoint, assertion));
    }

    // Nothing validates RealmOptions when the snapshot is published, so an out-of-range ceiling reaches the
    // evaluator as persisted. It must refuse rather than honour a value above the option's maximum, and rather
    // than crash on a negative one — where `now + lifetime` overflows.
    [Theory]
    [InlineData(-10)]
    [InlineData(120)]
    public async Task CeilingOutsideTheAcceptedRange_RefusesTheAssertionWithoutCrashing(int configuredMinutes)
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync(factory.Handles.Demo, $"pkj_bad_ceiling_{configuredMinutes}_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);

        await factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.Authentication.ClientAssertionMaxLifetime =
                TimeSpan.FromMinutes(configuredMinutes));
        try
        {
            var response = await PresentAsync(http, tokenEndpoint, CreateAssertion(
                clientId, tokenEndpoint, $"pkj-bad-ceiling-{configuredMinutes}-jti"));

            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            await AssertRefusedAsCredentialAsync(response);
        }
        finally
        {
            await factory.UpdateRealmAsync(
                factory.Handles.Demo,
                options => options.Authentication.ClientAssertionMaxLifetime =
                    Server.DefaultClientAssertionMaxLifetime);
        }
    }

    [Fact]
    public async Task RealmCeiling_IsConfigurable()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync(factory.Handles.Demo, "pkj_configured_ceiling_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);

        await factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.Authentication.ClientAssertionMaxLifetime = TimeSpan.FromMinutes(30));
        try
        {
            var response = await PresentAsync(http, tokenEndpoint, CreateAssertion(
                clientId, tokenEndpoint, "pkj-configured-ceiling-jti", TimeSpan.FromMinutes(20)));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await factory.UpdateRealmAsync(
                factory.Handles.Demo,
                options => options.Authentication.ClientAssertionMaxLifetime =
                    Server.DefaultClientAssertionMaxLifetime);
        }
    }

    // DF7 / invariant 5: a request refused for its shape must not have reached the evaluator at all, or a
    // malformed request would burn the jti of the assertion it carried and deny the client's next legitimate
    // attempt. Here the same assertion is first sent alongside a client_secret — two mechanisms, which is
    // malformed — and then on its own, where it must still be accepted.
    [Fact]
    public async Task AMalformedRequestCarryingAnAssertion_RegistersNoHandle()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync(factory.Handles.Demo, "pkj_preflight_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);
        var assertion = CreateAssertion(clientId, tokenEndpoint, "pkj-preflight-jti");

        var malformed = await http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_assertion_type"] = Oidc.ClientAssertionTypes.JwtBearer,
                ["client_assertion"] = assertion,
                ["client_secret"] = "a-second-mechanism",
                ["scope"] = "api",
            }));

        var legitimate = await PresentAsync(http, tokenEndpoint, assertion);

        await malformed.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
        Assert.Equal(HttpStatusCode.OK, legitimate.StatusCode);
    }

    // Invariant 5: the refusal must be diagnosable without the credential itself ending up in the log.
    [Fact]
    public async Task RefusingAReplay_LeaksNeitherTheAssertionNorTheIdentifier()
    {
        var http = factory.CreateClient();
        var clientId = await SaveClientAsync(factory.Handles.Demo, "pkj_no_leak_client");
        var tokenEndpoint = await GetTokenEndpointAsync(http, factory.Handles.Demo);
        var jti = $"pkj-no-leak-{Guid.NewGuid():N}";
        var assertion = CreateAssertion(clientId, tokenEndpoint, jti);

        await PresentAsync(http, tokenEndpoint, assertion);
        factory.ClearLog();
        var replay = await PresentAsync(http, tokenEndpoint, assertion);

        await AssertRefusedAsCredentialAsync(replay);

        var log = factory.AllLogText;
        Assert.DoesNotContain(jti, log, StringComparison.Ordinal);
        Assert.DoesNotContain(assertion, log, StringComparison.Ordinal);
    }

    private Task<string> SaveClientAsync(TestRealmHandle realm, string clientId)
        => factory.SaveClientAsync(realm, clientId, configured =>
        {
            configured.Name = clientId;
            configured.ClientType = ClientType.Confidential;
            configured.RequireClientSecret = true;
            configured.AllowAllResourceServers = true;
            configured.AllowedGrantTypes.Clear();
            configured.AllowedGrantTypes.Add("client_credentials");
            configured.Secrets.Add(key.CreateClientSecret());
        }).ContinueWith(_ => clientId, TaskContinuationOptions.OnlyOnRanToCompletion);

    private string CreateAssertion(
        string clientId, string tokenEndpoint, string jti, TimeSpan? lifetime = null)
    {
        var now = DateTimeOffset.UtcNow;
        return key.CreateAssertion(
            clientId, tokenEndpoint, jti, now, now + (lifetime ?? TimeSpan.FromMinutes(5)));
    }

    private static async Task<string> GetTokenEndpointAsync(HttpClient http, TestRealmHandle realm)
    {
        var discovery = await http.GetFromJsonAsync<Dictionary<string, JsonElement>>(
            $"/{realm.Path}/.well-known/openid-configuration");

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

    private static async Task AssertRefusedAsCredentialAsync(HttpResponseMessage response)
    {
        // The assertion is not presented through the Authorization header, so the refusal is a plain 400.
        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidClient);
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
