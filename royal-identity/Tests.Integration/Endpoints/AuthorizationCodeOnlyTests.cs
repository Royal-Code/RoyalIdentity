using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Proves the authorization-code-only boundary introduced by RFC 9700 hardening and the issuer identification
/// carried by every bounded authorization response according to RFC 9207.
/// </summary>
public class AuthorizationCodeOnlyTests : IClassFixture<PersistentStorageAppFactory>
{
    private static readonly Uri PublicOrigin = new("https://identity.royal.test");
    private readonly PersistentStorageAppFactory factory;

    public AuthorizationCodeOnlyTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Discovery_AdvertisesOnlyTheExecutableAuthorizationResponseAndExactPkceCapabilities()
    {
        var client = CreateClient();

        var metadata = await client.GetFromJsonAsync<JsonElement>(
            Oidc.Routes.BuildDiscoveryConfigurationUrl(factory.Handles.Demo.Path));

        Assert.Equal(
            [Oidc.ResponseTypes.Code],
            ReadStringArray(metadata.GetProperty(Oidc.Discovery.ResponseTypesSupported)));
        Assert.Equal(
            [Oidc.CodeChallenge.Methods.Plain, Oidc.CodeChallenge.Methods.Sha256],
            ReadStringArray(metadata.GetProperty(Oidc.Discovery.CodeChallengeMethodsSupported)));
        Assert.True(metadata.GetProperty(Oidc.Discovery.AuthorizationResponseIssParameterSupported).GetBoolean());
    }

    [Theory]
    [InlineData(Oidc.ResponseTypes.Token)]
    [InlineData(Oidc.ResponseTypes.IdToken)]
    [InlineData("code token")]
    [InlineData("code id_token")]
    [InlineData("code token id_token")]
    public async Task LegacyAllowedResponseTypes_CannotReenableImplicitOrHybrid(string responseType)
    {
        var clientId = $"legacy-response-{CryptoRandom.CreateUniqueId(5, OutputFormat.Hex)}";
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, configured =>
        {
            configured.Name = "Legacy response client";
            configured.RequireClientSecret = false;
            configured.RequirePkce = false;
            configured.AllowedGrantTypes.UnionWith(["authorization_code", "implicit"]);
            configured.AllowedIdentityScopes.UnionWith(["openid", "profile"]);
            configured.AllowedResponseTypes.UnionWith(
                responseType.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            configured.RedirectUris.Add("https://client.royal.test/callback");
        });

        var response = await CreateClient().GetAsync(
            BuildAuthorizeUrl(clientId, responseType, "https://client.royal.test/callback"));

        var error = await response.AssertErrorAsync(Oidc.Authorize.Errors.UnsupportedResponseType);
        Assert.DoesNotContain("access_token", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("id_token", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task SuccessfulResponse_ContainsOnlyCodeStateSessionStateAndTheRealmIssuer()
    {
        var client = CreateClient();
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var response = await client.GetAsync(BuildAuthorizeUrl(
            factory.Handles.DemoClient.ClientId,
            Oidc.ResponseTypes.Code,
            "https://localhost:5000/callback",
            state: "caller-state"));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var parameters = HttpUtility.ParseQueryString(response.Headers.Location!.Query);
        Assert.False(string.IsNullOrEmpty(parameters[Oidc.Authorize.Response.Code]));
        Assert.Equal("caller-state", parameters[Oidc.Authorize.Response.State]);
        Assert.False(string.IsNullOrEmpty(parameters[Oidc.Authorize.Response.SessionState]));
        Assert.Equal(
            $"{PublicOrigin.ToString().TrimEnd('/')}/{factory.Handles.Demo.Path}",
            parameters[Oidc.Authorize.Response.Issuer]);
        Assert.Null(parameters["access_token"]);
        Assert.Null(parameters["id_token"]);
    }

    [Fact]
    public async Task ErrorResponse_ContainsTheSameRealmIssuerPublishedByDiscovery()
    {
        var client = CreateClient();
        var metadata = await client.GetFromJsonAsync<JsonElement>(
            Oidc.Routes.BuildDiscoveryConfigurationUrl(factory.Handles.Demo.Path));

        var response = await client.GetAsync(BuildAuthorizeUrl(
            factory.Handles.DemoClient.ClientId,
            Oidc.ResponseTypes.Code,
            "https://localhost:5000/callback",
            prompt: Oidc.PromptModes.None));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var parameters = HttpUtility.ParseQueryString(response.Headers.Location!.Query);
        Assert.Equal(Oidc.Authorize.Errors.LoginRequired, parameters[Oidc.Authorize.Response.Error]);
        Assert.Equal(
            metadata.GetProperty(Oidc.Discovery.Issuer).GetString(),
            parameters[Oidc.Authorize.Response.Issuer]);
    }

    [Fact]
    public async Task PasswordGrant_RemainsUnsupportedEvenWhenResidualClientConfigurationAllowsIt()
    {
        var clientId = $"password-residual-{CryptoRandom.CreateUniqueId(5, OutputFormat.Hex)}";
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, configured =>
        {
            configured.Name = "Residual password client";
            configured.RequireClientSecret = false;
            configured.AllowedGrantTypes.Add("password");
        });

        var response = await CreateClient().PostAsync(
            Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Oidc.Token.Request.GrantType] = "password",
                [Oidc.Token.Request.ClientId] = clientId,
                ["username"] = "alice",
                ["password"] = "alice",
            }));

        await response.AssertErrorAsync(Oidc.Token.Errors.UnsupportedGrantType);
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = PublicOrigin,
        });

    private string BuildAuthorizeUrl(
        string clientId,
        string responseType,
        string redirectUri,
        string? state = null,
        string? prompt = null)
    {
        var url = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString(Oidc.Authorize.Request.ClientId, clientId)
            .AddQueryString(Oidc.Authorize.Request.ResponseType, responseType)
            .AddQueryString(Oidc.Authorize.Request.ResponseMode, Oidc.ResponseModes.Query)
            .AddQueryString(Oidc.Authorize.Request.Scope, "openid profile")
            .AddQueryString(Oidc.Authorize.Request.RedirectUri, redirectUri)
            .AddQueryString(Oidc.Authorize.Request.CodeChallenge, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString(Oidc.Authorize.Request.CodeChallengeMethod, Oidc.CodeChallenge.Methods.Sha256);

        if (state is not null)
            url = url.AddQueryString(Oidc.Authorize.Request.State, state);

        if (prompt is not null)
            url = url.AddQueryString(Oidc.Authorize.Request.Prompt, prompt);

        return url;
    }

    private static string[] ReadStringArray(JsonElement array)
        => array.EnumerateArray()
            .Select(value => value.GetString() ?? throw new JsonException("Metadata array contains null."))
            .ToArray();
}
