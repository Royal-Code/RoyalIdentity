using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// The opaque-bearer path must accept only reference access tokens.
/// <para>
/// A bearer without a dot is routed to reference validation, and the store is keyed by <c>jti</c> — which for a
/// JWT travels in its own (unencrypted) payload whenever <c>Client.IncludeJwtId</c> is on, as it is by default.
/// Without a type check, anyone holding a JWT access token could read its <c>jti</c> and present it as a second
/// bearer, skipping signature validation. Persisting a JWT — by the transitional in-memory backing, or by the
/// EF provider under <c>Metadata</c>/<c>Full</c> (plan DF31) — must never turn it into an opaque credential.
/// </para>
/// </summary>
public class ReferenceTokenBearerTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public ReferenceTokenBearerTests(PersistentStorageAppFactory factory) => this.factory = factory;

    private string UserInfoUrl => Oidc.Routes.BuildUserInfoUrl(factory.Handles.Demo.Path);

    private async Task<HttpResponseMessage> GetUserInfoAsync(HttpClient client, string bearer)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);

        return await client.SendAsync(message);
    }

    private async Task<IAsyncDisposable> UseJwtPersistenceAsync()
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var previous = realm.Options.OperationalStorage.JwtAccessTokenPersistence;
        await factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.OperationalStorage.JwtAccessTokenPersistence =
                JwtAccessTokenPersistenceMode.Metadata);
        return new AsyncRevert(() => factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.OperationalStorage.JwtAccessTokenPersistence = previous));
    }

    /// <summary>Reads a claim straight from the JWT payload — exactly what a token holder can do.</summary>
    private static string ReadJwtClaim(string jwt, string claimType)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=')
            .Replace('-', '+')
            .Replace('_', '/');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        return JsonDocument.Parse(json).RootElement.GetProperty(claimType).GetString()!;
    }

    // The regression: the jti of a persisted JWT is not a bearer, and it is rejected exactly like a handle that
    // does not exist — so the response is no oracle about which jtis are stored.
    [Fact]
    public async Task JwtAccessToken_JtiPresentedAsAnOpaqueBearer_IsRejectedLikeAnUnknownHandle()
    {
        await using var persistence = await UseJwtPersistenceAsync();
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var accessToken = (await client.GetTokensAsync()).AccessToken!;
        var jti = ReadJwtClaim(accessToken, "jti");

        // A jti has no dot, so it is routed to the reference-token path — where the type check must stop it.
        Assert.DoesNotContain('.', jti);

        var withJti = await GetUserInfoAsync(client, jti);
        var withUnknownHandle = await GetUserInfoAsync(client, "handle-that-was-never-issued");

        Assert.NotEqual(HttpStatusCode.OK, withJti.StatusCode);
        Assert.Equal(withUnknownHandle.StatusCode, withJti.StatusCode);
        Assert.Equal(
            await withUnknownHandle.Content.ReadAsStringAsync(),
            await withJti.Content.ReadAsStringAsync());
    }

    // The same JWT keeps working, so the check did not narrow the legitimate path.
    [Fact]
    public async Task JwtAccessToken_ItselfIsStillAccepted()
    {
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var accessToken = (await client.GetTokensAsync()).AccessToken!;

        var response = await GetUserInfoAsync(client, accessToken);

        Assert.True(response.IsSuccessStatusCode);
    }

    // A genuine reference token is still accepted. Emission hardcodes JWT today, so the token is seeded through
    // the store — otherwise nothing in the suite would notice if the reference path stopped working.
    [Fact]
    public async Task ReferenceAccessToken_IsAccepted()
    {
        await using var persistence = await UseJwtPersistenceAsync();
        var client = factory.CreateClient();
        await client.LoginAliceAsync();
        var jwt = (await client.GetTokensAsync()).AccessToken!;

        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var issued = await factory.WithStorageAsync(
            storage => storage.GetAccessTokenStore(realm).GetAsync(ReadJwtClaim(jwt, "jti"), default));
        Assert.NotNull(issued);

        // Same subject, client and claims as the JWT above, but issued as a reference token.
        const string bearer = "reference-bearer-for-userinfo";
        var reference = new AccessToken(
            issued.ClientId,
            issued.Issuer,
            AccessTokenType.Reference,
            issued.CreationTime,
            issued.Lifetime,
            bearer,
            issued.TokenType)
        {
            RealmId = issued.RealmId,
        };
        foreach (var claim in issued.Claims.Where(claim => claim.Type != "jti"))
            reference.Claims.Add(claim);

        await factory.WithStorageAsync(
            storage => storage.GetAccessTokenStore(realm).StoreAsync(reference, default));

        var response = await GetUserInfoAsync(client, bearer);

        Assert.True(response.IsSuccessStatusCode);
    }

    private sealed class AsyncRevert(Func<Task> revert) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => new(revert());
    }
}
