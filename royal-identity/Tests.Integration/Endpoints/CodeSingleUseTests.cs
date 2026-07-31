using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Flow-level behavior of the single-use authorization code (plan Fase 4, MP-2/DF11): the code is consumed on
/// the first exchange whatever happens afterwards, an unmatched client or redirect URI consumes nothing, and
/// none of these outcomes is distinguishable from another.
/// <para>
/// These run over the canonical EF backing and therefore exercise the atomic consume path. The dedicated
/// concurrency acceptance remains in <c>Tests.Storage</c>.
/// </para>
/// </summary>
public class CodeSingleUseTests : IClassFixture<PersistentStorageAppFactory>
{
    private static readonly string[] ScopeNames = ["openid", "profile"];

    private const string ClientId = "demo_client";

    /// <summary>Another registered client, so a binding mismatch is not confused with a failed authentication.</summary>
    private const string OtherClientId = "demo_consent_client";

    private const string RedirectUri = "http://localhost:5000/callback";

    private readonly PersistentStorageAppFactory factory;

    public CodeSingleUseTests(PersistentStorageAppFactory factory) => this.factory = factory;

    private async Task<AuthorizationCode> SeedCodeAsync(
        string? codeChallenge = null, string? codeChallengeMethod = null, DateTime? creationTime = null)
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        return await factory.WithStorageAsync(async storage =>
        {
            var resources = await storage.GetResourceStore(realm)
                .FindResourcesByScopeAsync(ScopeNames, default);

            var code = new AuthorizationCode(
                ClientId,
                SubjectFactory.CreateWithSession(
                    storage, realm, factory.Handles.Alice.SubjectId, "Test Name", "admin"),
                "session",
                creationTime ?? DateTime.UtcNow,
                300,
                resources,
                RedirectUri)
            {
                CodeChallenge = codeChallenge,
                CodeChallengeMethod = codeChallengeMethod,
            };

            await storage.GetAuthorizationCodeStore(realm)
                .StoreAuthorizationCodeAsync(code, default);
            return code;
        });
    }

    private async Task<HttpResponseMessage> ExchangeAsync(
        string code,
        string clientId = ClientId,
        string redirectUri = RedirectUri,
        string? codeVerifier = null)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
        };

        if (codeVerifier is not null)
            form["code_verifier"] = codeVerifier;

        return await factory.CreateClient().PostAsync(
            Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path), new FormUrlEncodedContent(form));
    }

    // The core rule: a code works once.
    [Fact]
    public async Task ExchangingTheSameCodeTwice_SucceedsOnlyTheFirstTime()
    {
        var code = await SeedCodeAsync();

        var first = await ExchangeAsync(code.Code);
        var second = await ExchangeAsync(code.Code);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        await second.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);
    }

    // DF11: a mismatched binding does not consume the code, so the legitimate exchange still works afterwards.
    // An invalid request must not be able to deny the rightful one. Both clients here are registered and
    // authenticate fine — the point is the binding of the code, not client authentication.
    [Theory]
    [InlineData(OtherClientId, RedirectUri)]
    [InlineData(ClientId, "http://localhost:5000/other-callback")]
    public async Task AMismatchedBinding_DoesNotConsumeTheCode(string clientId, string redirectUri)
    {
        var code = await SeedCodeAsync();

        var rejected = await ExchangeAsync(code.Code, clientId, redirectUri);
        var legitimate = await ExchangeAsync(code.Code);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.OK, legitimate.StatusCode);
    }

    // DF11 (observable change): a redirect mismatch no longer answers "Invalid redirect_uri". All four ways a
    // code exchange can be refused — never issued, already consumed, wrong client, wrong redirect URI — answer
    // with the same error and the same description, so the response is no oracle about which codes exist or who
    // they belong to. The OAuth code stays invalid_grant.
    [Fact]
    public async Task EveryRefusedExchange_AnswersIdentically()
    {
        var consumed = await SeedCodeAsync();
        await ExchangeAsync(consumed.Code);
        var bound = await SeedCodeAsync();

        var unknownCode = await (await ExchangeAsync("code-that-was-never-issued")).ReadErrorAsync();
        var alreadyConsumed = await (await ExchangeAsync(consumed.Code)).ReadErrorAsync();
        var clientMismatch = await (await ExchangeAsync(bound.Code, clientId: OtherClientId)).ReadErrorAsync();
        var redirectMismatch = await (
            await ExchangeAsync(bound.Code, redirectUri: "http://localhost:5000/other-callback")).ReadErrorAsync();

        Assert.Equal(Oidc.Token.Errors.InvalidGrant, unknownCode.Error);
        Assert.Equal(unknownCode.Answer, alreadyConsumed.Answer);
        Assert.Equal(unknownCode.Answer, clientMismatch.Answer);
        Assert.Equal(unknownCode.Answer, redirectMismatch.Answer);
        Assert.DoesNotContain("redirect", unknownCode.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // DF11 end to end: an expired code is still consumed, and only then rejected — so the caller learns it
    // expired, and a second attempt gets the generic refusal because the code is already gone.
    [Fact]
    public async Task AnExpiredCode_IsRejectedAsExpired_AndConsumedAllTheSame()
    {
        var code = await SeedCodeAsync(creationTime: DateTime.UtcNow.AddHours(-1));

        var first = await (await ExchangeAsync(code.Code)).ReadErrorAsync();
        var second = await (await ExchangeAsync(code.Code)).ReadErrorAsync();
        var unknownCode = await (await ExchangeAsync("code-that-was-never-issued")).ReadErrorAsync();

        Assert.Equal(Oidc.Token.Errors.InvalidGrant, first.Error);
        Assert.Contains("expired", first.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        // The expiry answer is only given once: the code was consumed, so the retry is the generic refusal.
        Assert.Equal(unknownCode.Answer, second.Answer);
    }

    // DF11: PKCE is validated after the consumption, so a failed verifier does not hand the code back — a
    // second attempt with the right verifier must not work.
    [Fact]
    public async Task AFailedPkceVerification_StillConsumesTheCode()
    {
        var verifier = CryptoRandom.CreateUniqueId();
        var code = await SeedCodeAsync(PkceHelper.GenerateStoredS256CodeChallengeHash(verifier), "S256");

        var wrongVerifier = await ExchangeAsync(code.Code, codeVerifier: CryptoRandom.CreateUniqueId());
        var rightVerifier = await ExchangeAsync(code.Code, codeVerifier: verifier);

        Assert.Equal(HttpStatusCode.BadRequest, wrongVerifier.StatusCode);
        // The code is gone: winning the code and then failing PKCE never makes it reusable.
        Assert.Equal(HttpStatusCode.BadRequest, rightVerifier.StatusCode);
        await rightVerifier.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);
    }
}
