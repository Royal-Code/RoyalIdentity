// Ignore Spelling: Pkce

using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// The PKCE rows of the normative matrix, over real authorization codes.
/// </summary>
/// <remarks>
/// OAuth 2.1 draft-15 §§3.2.4/4.1.3 and RFC 7636 §4.6 split the failures by which question failed. Verifier and
/// challenge disagreeing about <b>presence</b> is a malformed request; a verifier that was presented and does
/// not <b>match</b> is an invalid grant. The second is the only one an attacker can reach with a stolen code,
/// and it must answer exactly like every other refusal of a presented code.
/// </remarks>
public class PkceTokenTests : IClassFixture<LogCapturingAppFactory>
{
    private static readonly string[] ScopeNames = ["openid", "profile"];

    private const string ClientId = "demo_client";
    private const string RedirectUri = "http://localhost:5000/callback";

    private readonly LogCapturingAppFactory factory;

    public PkceTokenTests(LogCapturingAppFactory factory) => this.factory = factory;

    private async Task<AuthorizationCode> SeedCodeAsync(
        string? codeChallenge = null,
        string? codeChallengeMethod = null)
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
                DateTime.UtcNow,
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

    private Task<HttpResponseMessage> ExchangeAsync(string code, string? codeVerifier = null)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
        };

        if (codeVerifier is not null)
            form["code_verifier"] = codeVerifier;

        return factory.CreateClient().PostAsync(
            Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path), new FormUrlEncodedContent(form));
    }

    // Draft-15 §3.2.4 added this one explicitly. Accepting it in silence is the PKCE downgrade: an attacker
    // holding a stolen code strips the challenge binding by simply not having one.
    [Fact]
    public async Task AVerifierPresentedForACodeWithoutChallenge_IsRefusedAsMalformed()
    {
        var code = await SeedCodeAsync();

        var response = await ExchangeAsync(code.Code, CryptoRandom.CreateUniqueId());

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    // DF9/DF11: presenting the parameter is the key being there, not the value being usable. An empty or blank
    // code_verifier was sent, so verifier and challenge still disagree about presence and the request is
    // malformed — deciding it by the value would let a code without challenge be exchanged in silence.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyVerifierPresentedForACodeWithoutChallenge_IsRefusedAsMalformed(string verifier)
    {
        var code = await SeedCodeAsync();

        var response = await ExchangeAsync(code.Code, verifier);

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task ACodeWithChallengeExchangedWithoutVerifier_IsRefusedAsMalformed()
    {
        var verifier = CryptoRandom.CreateUniqueId();
        var code = await SeedCodeAsync(PkceHelper.GenerateStoredS256CodeChallengeHash(verifier), "S256");

        var response = await ExchangeAsync(code.Code);

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task BothDirectionsOfThePresenceMismatch_AnswerIdentically()
    {
        // They are the same defect seen from two sides, and neither says anything about the code itself.
        var verifier = CryptoRandom.CreateUniqueId();
        var withoutChallenge = await SeedCodeAsync();
        var withChallenge = await SeedCodeAsync(
            PkceHelper.GenerateStoredS256CodeChallengeHash(verifier), "S256");

        var verifierWithoutChallenge = await (
            await ExchangeAsync(withoutChallenge.Code, CryptoRandom.CreateUniqueId())).ReadErrorAsync();
        var challengeWithoutVerifier = await (await ExchangeAsync(withChallenge.Code)).ReadErrorAsync();

        Assert.Equal(Oidc.Token.Errors.InvalidRequest, verifierWithoutChallenge.Error);
        Assert.Equal(verifierWithoutChallenge.Answer, challengeWithoutVerifier.Answer);
    }

    // DF9 plus RFC 7636 §4.1: a verifier whose syntax is wrong never became a comparable value, so it is a
    // malformed request rather than a failed grant. Every other core parameter already worked this way; the
    // verifier was the one that compared first and never checked, turning "you sent three characters" into
    // "your grant is invalid".
    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "whitespace")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "42 characters, one below the minimum")]
    [InlineData(
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" +
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        "129 characters, one above the maximum")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+", "a character outside the unreserved set")]
    public async Task AMalformedVerifier_IsRefusedAsMalformed_NotAsAFailedGrant(string verifier, string _)
    {
        var storedVerifier = CryptoRandom.CreateUniqueId();
        var code = await SeedCodeAsync(
            PkceHelper.GenerateStoredS256CodeChallengeHash(storedVerifier), "S256");

        var response = await ExchangeAsync(code.Code, verifier);

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
    }

    [Fact]
    public async Task AVerifierOfExactlyTheMinimumAndMaximumLength_IsAccepted_AsSyntax()
    {
        // The bounds are inclusive, so 43 and 128 must reach the comparison and fail there, not at the syntax
        // check — otherwise a legitimate client at the edge of the range would be told its request is
        // malformed.
        var storedVerifier = CryptoRandom.CreateUniqueId();
        var atMinimum = await SeedCodeAsync(
            PkceHelper.GenerateStoredS256CodeChallengeHash(storedVerifier), "S256");
        var atMaximum = await SeedCodeAsync(
            PkceHelper.GenerateStoredS256CodeChallengeHash(storedVerifier), "S256");

        var minimum = await (await ExchangeAsync(atMinimum.Code, new string('a', 43))).ReadErrorAsync();
        var maximum = await (await ExchangeAsync(atMaximum.Code, new string('a', 128))).ReadErrorAsync();

        Assert.Equal(Oidc.Token.Errors.InvalidGrant, minimum.Error);
        Assert.Equal(Oidc.Token.Errors.InvalidGrant, maximum.Error);
    }

    [Fact]
    public async Task AVerifierThatDoesNotMatch_IsRefusedAsAnInvalidGrant()
    {
        // RFC 7636 §4.6: the verifier was presented and is wrong, which is a failed grant and not a malformed
        // request. This is the row a stolen code actually reaches.
        var verifier = CryptoRandom.CreateUniqueId();
        var code = await SeedCodeAsync(PkceHelper.GenerateStoredS256CodeChallengeHash(verifier), "S256");

        var response = await ExchangeAsync(code.Code, CryptoRandom.CreateUniqueId());

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);
    }

    // DF18: a code_challenge_method the server cannot process is a corrupted record or a bad seed. The client
    // presented an artifact the server cannot honour, and that is a protocol answer — never a 5xx, which would
    // report a request the server refused as an outage.
    [Fact]
    public async Task AnUnsupportedStoredMethod_IsRefusedAsAnInvalidGrant_NotAsAServerError()
    {
        var code = await SeedCodeAsync("some-stored-challenge", "urn:example:not-a-method");

        var response = await ExchangeAsync(code.Code, CryptoRandom.CreateUniqueId());

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);
    }

    [Fact]
    public async Task EveryRefusalOfAPresentedCode_AnswersIdentically()
    {
        // The equivalence DF13/DF18 require: a wrong verifier, a method the server cannot process and a code
        // that was never issued are one single answer. Telling them apart would let a caller learn whether a
        // guessed verifier was the only thing missing, or that it had found a code the server had broken.
        var verifier = CryptoRandom.CreateUniqueId();
        var withChallenge = await SeedCodeAsync(
            PkceHelper.GenerateStoredS256CodeChallengeHash(verifier), "S256");
        var withUnsupportedMethod = await SeedCodeAsync("some-stored-challenge", "urn:example:not-a-method");

        var wrongVerifier = await (
            await ExchangeAsync(withChallenge.Code, CryptoRandom.CreateUniqueId())).ReadErrorAsync();
        var unsupportedMethod = await (
            await ExchangeAsync(withUnsupportedMethod.Code, CryptoRandom.CreateUniqueId())).ReadErrorAsync();
        var unknownCode = await (
            await ExchangeAsync("code-that-was-never-issued", CryptoRandom.CreateUniqueId())).ReadErrorAsync();

        Assert.Equal(Oidc.Token.Errors.InvalidGrant, wrongVerifier.Error);
        Assert.Equal(wrongVerifier.Answer, unsupportedMethod.Answer);
        Assert.Equal(wrongVerifier.Answer, unknownCode.Answer);
    }

    [Fact]
    public async Task AnUnsupportedStoredMethod_IsNamedInTheLog_AndNowhereInTheResponse()
    {
        // DF18 keeps the method out of the response and in the log: without it, a corrupted record or a bad
        // seed would be indistinguishable from ordinary wrong-verifier traffic and never get diagnosed.
        const string method = "urn:example:not-a-method";
        var code = await SeedCodeAsync("some-stored-challenge", method);

        factory.ClearLog();
        var response = await ExchangeAsync(code.Code, CryptoRandom.CreateUniqueId());

        var error = await response.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);

        Assert.DoesNotContain(method, error.Description ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(method, factory.AllLogText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedPresenceCheck_StillConsumesTheCode()
    {
        // The consumption happens in LoadCode, before this validator runs. Moving the presence check earlier
        // must not have handed the code back: a refused attempt that leaves the code usable is a retry oracle.
        var verifier = CryptoRandom.CreateUniqueId();
        var code = await SeedCodeAsync(PkceHelper.GenerateStoredS256CodeChallengeHash(verifier), "S256");

        var withoutVerifier = await ExchangeAsync(code.Code);
        var withTheRightVerifier = await ExchangeAsync(code.Code, verifier);

        await withoutVerifier.AssertErrorAsync(Oidc.Token.Errors.InvalidRequest);
        await withTheRightVerifier.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);
    }

    // Invariant 9: a refusal has to be diagnosable without the artifacts themselves ending up in the log. The
    // authorization code and the code_verifier are single-use credentials, and a log is read by more people,
    // for longer, than a response ever is.
    [Fact]
    public async Task RefusingAnExchange_LeaksNeitherTheCodeNorTheVerifier()
    {
        var storedVerifier = CryptoRandom.CreateUniqueId();
        var code = await SeedCodeAsync(
            PkceHelper.GenerateStoredS256CodeChallengeHash(storedVerifier), "S256");
        var presentedVerifier = CryptoRandom.CreateUniqueId();

        factory.ClearLog();
        var response = await ExchangeAsync(code.Code, presentedVerifier);

        await response.AssertErrorAsync(Oidc.Token.Errors.InvalidGrant);

        var log = factory.AllLogText;
        Assert.DoesNotContain(code.Code, log, StringComparison.Ordinal);
        Assert.DoesNotContain(presentedVerifier, log, StringComparison.Ordinal);
    }

    // The seeded-code tests above never reach ICodeFactory, so they cannot see what issuance logs. This one
    // drives the real authorization flow: the code that comes back in the redirect must not appear anywhere in
    // the log, which is where it was being written in clear by DefaultCodeFactory.
    [Fact]
    public async Task IssuingACodeThroughTheAuthorizationFlow_LeaksItNowhereInTheLog()
    {
        var options = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };
        var http = factory.CreateClient(options);
        await http.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var verifier = CryptoRandom.CreateUniqueId();
        var path = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", ClientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid profile")
            .AddQueryString("redirect_uri", RedirectUri)
            .AddQueryString("state", "state")
            .AddQueryString("code_challenge", PkceHelper.GenerateS256CodeChallenge(verifier))
            .AddQueryString("code_challenge_method", "S256");

        factory.ClearLog();
        var response = await http.GetAsync(path);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var query = HttpUtility.ParseQueryString(response.Headers.Location!.ToString().Split('?')[1]);
        var issuedCode = query["code"];

        Assert.NotNull(issuedCode);
        Assert.DoesNotContain(issuedCode, factory.AllLogText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRightVerifier_StillWorks()
    {
        // The control every taxonomy change needs: none of the refusals above came from breaking the success.
        var verifier = CryptoRandom.CreateUniqueId();
        var code = await SeedCodeAsync(PkceHelper.GenerateStoredS256CodeChallengeHash(verifier), "S256");

        var response = await ExchangeAsync(code.Code, verifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
