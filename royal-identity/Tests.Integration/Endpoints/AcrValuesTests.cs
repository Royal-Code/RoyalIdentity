using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Proves that <c>acr_values</c> remains an ordered client preference and never becomes an authentication claim
/// or a promise of capabilities that the server does not implement.
/// </summary>
public class AcrValuesTests : IClassFixture<PersistentStorageAppFactory>
{
    public static TheoryData<string, string[]> OrderedPreferences => new()
    {
        { "urn:example:loa1", ["urn:example:loa1"] },
        { "urn:example:loa2 urn:example:loa1", ["urn:example:loa2", "urn:example:loa1"] },
        {
            "loa2 loa1 loa2 LOA2 loa1",
            ["loa2", "loa1", "LOA2"]
        },
        { "idp:partner urn:unknown:acr", ["idp:partner", "urn:unknown:acr"] },
    };

    private readonly PersistentStorageAppFactory factory;

    public AcrValuesTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void PublicBoundaries_ExposeAcrValuesAsAnOrderedReadOnlyList()
    {
        Assert.Equal(
            typeof(IReadOnlyList<string>),
            typeof(IWithAcr).GetProperty(nameof(IWithAcr.AcrValues))!.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<string>),
            typeof(AuthorizeContext).GetProperty(nameof(AuthorizeContext.AcrValues))!.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<string>),
            typeof(RoyalIdentity.Users.Contexts.AuthorizationContext)
                .GetProperty(nameof(RoyalIdentity.Users.Contexts.AuthorizationContext.AcrValues))!
                .PropertyType);
    }

    [Theory]
    [MemberData(nameof(OrderedPreferences))]
    public async Task Validate_PreservesFirstOrdinalOccurrenceAtTheInteractionBoundary(
        string rawAcrValues,
        string[] expected)
    {
        var result = await ValidateAsync(rawAcrValues);

        Assert.Null(result.Error);
        Assert.NotNull(result.Context);
        Assert.Equal(expected, result.Context.AcrValues);
    }

    [Fact]
    public async Task Validate_RejectsAcrValuesAboveTheRealmLengthLimit()
    {
        var (result, limit, length) = await ValidateAboveLimitAsync();

        Assert.True(result.Context is null, $"Expected length {length} to exceed the realm limit {limit}.");
        Assert.NotNull(result.Error);
        Assert.Equal(Oidc.Authorize.Errors.InvalidRequest, result.Error.Error);
        Assert.Equal("Invalid acr_values: too long", result.Error.ErrorDescription);
    }

    [Fact]
    public async Task Discovery_DoesNotPromiseSupportedAcrValues()
    {
        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildDiscoveryConfigurationUrl(factory.Handles.Demo.Path);
        using var document = JsonDocument.Parse(await client.GetStringAsync(url));

        Assert.False(document.RootElement.TryGetProperty(Oidc.Discovery.AcrValuesSupported, out _));
    }

    [Fact]
    public async Task AuthorizationRequestAcrValues_DoNotCreateAcrClaimsInIssuedTokens()
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var verifier = CryptoRandom.CreateUniqueId();
        var authorizeUrl = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString(Oidc.Authorize.Request.ClientId, factory.Handles.DemoClient.ClientId)
            .AddQueryString(Oidc.Authorize.Request.ResponseType, Oidc.ResponseTypes.Code)
            .AddQueryString(Oidc.Authorize.Request.ResponseMode, Oidc.ResponseModes.Query)
            .AddQueryString(Oidc.Authorize.Request.Scope, "openid profile")
            .AddQueryString(Oidc.Authorize.Request.RedirectUri, "https://localhost:5000/callback")
            .AddQueryString(Oidc.Authorize.Request.AcrValues, "urn:unknown:acr idp:partner")
            .AddQueryString(Oidc.Authorize.Request.CodeChallenge, PkceHelper.GenerateS256CodeChallenge(verifier))
            .AddQueryString(Oidc.Authorize.Request.CodeChallengeMethod, Oidc.CodeChallenge.Methods.Sha256);

        var authorizeResponse = await client.GetAsync(authorizeUrl);

        Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);
        var callback = Assert.IsType<Uri>(authorizeResponse.Headers.Location);
        var code = HttpUtility.ParseQueryString(callback.Query)[Oidc.Authorize.Response.Code];
        Assert.NotNull(code);

        var tokenResponse = await client.PostAsync(
            Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Oidc.Token.Request.GrantType] = OpenIdConnectGrantTypes.AuthorizationCode,
                [Oidc.Token.Request.Code] = code,
                [Oidc.Token.Request.ClientId] = factory.Handles.DemoClient.ClientId,
                [Oidc.Token.Request.RedirectUri] = "https://localhost:5000/callback",
                [Oidc.Token.Request.CodeVerifier] = verifier,
            }));

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        using var tokens = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var identityToken = tokens.RootElement.GetProperty(Oidc.Token.Response.IdentityToken).GetString();
        var accessToken = tokens.RootElement.GetProperty(Oidc.Token.Response.AccessToken).GetString();

        Assert.NotNull(identityToken);
        Assert.NotNull(accessToken);
        Assert.DoesNotContain(
            new JwtSecurityTokenHandler().ReadJwtToken(identityToken).Claims,
            claim => claim.Type == JwtRegisteredClaimNames.Acr);
        Assert.DoesNotContain(
            new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims,
            claim => claim.Type == JwtRegisteredClaimNames.Acr);
    }

    private async Task<AuthorizationValidationResult> ValidateAsync(string rawAcrValues)
    {
        using var scope = factory.Services.CreateScope();
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);

        return await ValidateAsync(scope, realm, rawAcrValues);
    }

    private async Task<(AuthorizationValidationResult Result, int Limit, int Length)> ValidateAboveLimitAsync()
    {
        using var scope = factory.Services.CreateScope();
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var tooLong = new string('a', realm.Options.InputLengthRestrictions.AcrValues + 1);

        return (
            await ValidateAsync(scope, realm, tooLong),
            realm.Options.InputLengthRestrictions.AcrValues,
            tooLong.Length);
    }

    private async Task<AuthorizationValidationResult> ValidateAsync(
        IServiceScope scope,
        RoyalIdentity.Models.Realm realm,
        string rawAcrValues)
    {
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("acr.contract.test");
        httpContext.Items[Server.RealmCurrentKey] = realm;

        var parameters = new NameValueCollection
        {
            [Oidc.Authorize.Request.ClientId] = factory.Handles.DemoClient.ClientId,
            [Oidc.Authorize.Request.ResponseType] = Oidc.ResponseTypes.Code,
            [Oidc.Authorize.Request.ResponseMode] = Oidc.ResponseModes.Query,
            [Oidc.Authorize.Request.Scope] = "openid profile",
            [Oidc.Authorize.Request.RedirectUri] = "https://localhost:5000/callback",
            [Oidc.Authorize.Request.AcrValues] = rawAcrValues,
            [Oidc.Authorize.Request.CodeChallenge] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            [Oidc.Authorize.Request.CodeChallengeMethod] = Oidc.CodeChallenge.Methods.Sha256,
        };

        return await scope.ServiceProvider
            .GetRequiredService<IAuthorizeRequestValidator>()
            .ValidateAsync(
                new AuthorizationValidationRequest
                {
                    HttpContext = httpContext,
                    Parameters = parameters,
                },
                default);
    }
}
