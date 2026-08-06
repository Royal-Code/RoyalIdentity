using System.Collections.Specialized;
using System.Net;
using System.Web;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Decorators;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Responses;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Fase 3 of plan-oidc-session-management: Authentication Responses own session_state and prompt=none never
/// falls through to an interactive page. The current subject model has one selected principal and no account
/// chooser, so account_selection_required has no reachable condition yet; this suite does not fabricate one.
/// </summary>
public class AuthorizeSessionStateTests : IClassFixture<ControlledTimeAppFactory>
{
    private readonly ControlledTimeAppFactory factory;

    public AuthorizeSessionStateTests(ControlledTimeAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task SuccessfulAuthenticationResponse_EmitsSessionStateOnlyForOpenId()
    {
        factory.ResetClock();
        var oauthClientId = $"oauth-session-state-{CryptoRandom.CreateUniqueId(5, OutputFormat.Hex)}";
        await factory.SaveClientAsync(factory.Handles.Demo, oauthClientId, configured =>
        {
            configured.Name = "OAuth session state client";
            configured.RequireClientSecret = false;
            configured.RequirePkce = false;
            configured.AllowedGrantTypes.Add("authorization_code");
            configured.AllowedResourceServers.Add("apiserver");
            configured.AllowedResponseTypes.Add("code");
            configured.RedirectUris.Add("http://localhost:5000/callback");
        });
        var client = CreateClient();
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var openId = await client.GetAsync(BuildAuthorizeUrl(scope: "openid profile"));
        var oauth = await client.GetAsync(BuildAuthorizeUrl(oauthClientId, "api:read"));

        var openIdParameters = AssertCallback(openId);
        var oauthParameters = AssertCallback(oauth);
        Assert.NotNull(openIdParameters["code"]);
        Assert.False(string.IsNullOrEmpty(openIdParameters["session_state"]));
        Assert.NotNull(oauthParameters["code"]);
        Assert.Null(oauthParameters["session_state"]);
    }

    [Fact]
    public async Task PromptNone_WithoutAuthenticatedSession_RedirectsLoginRequiredWithoutUi()
    {
        var response = await CreateClient().GetAsync(BuildAuthorizeUrl(
            ("prompt", "none"),
            ("state", "caller-state")));

        var parameters = AssertCallback(response);
        Assert.Equal("login_required", parameters["error"]);
        Assert.Equal("caller-state", parameters["state"]);
        Assert.Null(parameters["session_state"]);
        Assert.Null(parameters["code"]);
    }

    [Fact]
    public async Task PromptNone_Error_PreservesFormPostResponseModeAndState()
    {
        var response = await CreateClient().GetAsync(BuildAuthorizeUrl(
            ("prompt", "none"),
            ("response_mode", "form_post"),
            ("state", "form-post-state")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = new HtmlDocument();
        document.LoadHtml(await response.Content.ReadAsStringAsync());
        var form = Assert.Single(document.DocumentNode.SelectNodes("//form"));
        Assert.Equal("http://localhost:5000/callback", form.GetAttributeValue("action", ""));
        var inputs = form.SelectNodes(".//input")
            .ToDictionary(
                input => input.GetAttributeValue("name", ""),
                input => input.GetAttributeValue("value", ""));
        Assert.Equal("login_required", inputs["error"]);
        Assert.Equal("form-post-state", inputs["state"]);
        Assert.DoesNotContain("code", inputs.Keys);
    }

    [Theory]
    [InlineData("none login")]
    [InlineData("none unsupported_prompt")]
    public async Task PromptNone_CombinedWithAnotherRawValue_RedirectsInvalidRequest(string prompt)
    {
        var response = await CreateClient().GetAsync(BuildAuthorizeUrl(
            ("prompt", prompt),
            ("state", "caller-state")));

        var parameters = AssertCallback(response);
        Assert.Equal("invalid_request", parameters["error"]);
        Assert.Equal("caller-state", parameters["state"]);
        Assert.Null(parameters["code"]);
        Assert.DoesNotContain("account/login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizeContinuation_PromptNoneCombinedWithAnotherValue_ReturnsAValidationError()
    {
        using var scope = factory.Services.CreateScope();
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("continuation.contract.test");
        httpContext.Items[Server.RealmCurrentKey] = realm;
        var parameters = new NameValueCollection
        {
            [Oidc.Authorize.Request.ClientId] = factory.Handles.DemoClient.ClientId,
            [Oidc.Authorize.Request.ResponseType] = Oidc.ResponseTypes.Code,
            [Oidc.Authorize.Request.ResponseMode] = Oidc.ResponseModes.Query,
            [Oidc.Authorize.Request.Scope] = "openid profile",
            [Oidc.Authorize.Request.RedirectUri] = "http://localhost:5000/callback",
            [Oidc.Authorize.Request.Prompt] = "none login",
        };

        var result = await scope.ServiceProvider
            .GetRequiredService<IAuthorizeRequestValidator>()
            .ValidateAsync(
                new AuthorizationValidationRequest
                {
                    HttpContext = httpContext,
                    Parameters = parameters,
                },
                default);

        Assert.Null(result.Context);
        Assert.NotNull(result.Error);
        Assert.Equal(Oidc.Authorize.Errors.InvalidRequest, result.Error.Error);
        Assert.Equal("Invalid prompt", result.Error.ErrorDescription);
    }

    [Theory]
    [InlineData("demo_client", "https://attacker.example/callback")]
    [InlineData("demo_client", "http://localhost:5999/callback")]
    [InlineData("unknown-client", "http://localhost:5000/callback")]
    public async Task PromptNone_WithUntrustedClientOrRedirect_DoesNotRedirect(
        string clientId,
        string redirectUri)
    {
        var response = await CreateClient().GetAsync(BuildAuthorizeUrlWithBinding(
            clientId,
            redirectUri,
            ("prompt", "none")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task PromptNone_WhenAuthenticationIsTooOld_RedirectsLoginRequiredWithSessionState()
    {
        factory.ResetClock();
        var client = CreateClient();
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);
        factory.Clock.Advance(TimeSpan.FromSeconds(1));

        var response = await client.GetAsync(BuildAuthorizeUrl(
            ("prompt", "none"),
            ("max_age", "0")));

        var parameters = AssertCallback(response);
        Assert.Equal("login_required", parameters["error"]);
        Assert.False(string.IsNullOrEmpty(parameters["session_state"]));
        Assert.Null(parameters["code"]);
    }

    [Fact]
    public async Task PromptNone_WhenConsentIsRequired_RedirectsConsentRequiredWithSessionState()
    {
        factory.ResetClock();
        var clientId = $"prompt-none-consent-{CryptoRandom.CreateUniqueId(5, OutputFormat.Hex)}";
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, configured =>
        {
            configured.Name = "Prompt none consent client";
            configured.RequireClientSecret = false;
            configured.RequirePkce = false;
            configured.RequireConsent = true;
            configured.AllowRememberConsent = false;
            configured.AllowedGrantTypes.Add("authorization_code");
            configured.AllowedIdentityScopes.UnionWith(["openid", "profile"]);
            configured.AllowedResponseTypes.Add("code");
            configured.RedirectUris.Add("http://localhost:5000/callback");
        });
        var client = CreateClient();
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var response = await client.GetAsync(BuildAuthorizeUrl(
            clientId,
            "openid profile",
            ("prompt", "none")));

        var parameters = AssertCallback(response);
        Assert.Equal("consent_required", parameters["error"]);
        Assert.False(string.IsNullOrEmpty(parameters["session_state"]));
        Assert.Null(parameters["code"]);
    }

    [Fact]
    public async Task PromptSelectAccount_WithoutNone_RemainsInteractive()
    {
        factory.ResetClock();
        var client = CreateClient();
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);

        var response = await client.GetAsync(BuildAuthorizeUrl(("prompt", "select_account")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("account/login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PromptNone_CustomTerminalInteraction_IsConvertedToInteractionRequired()
    {
        using var scope = factory.Services.CreateScope();
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        var client = await scope.ServiceProvider.GetRequiredService<IStorage>()
            .GetClientStore(realm)
            .FindEnabledClientByIdAsync(factory.Handles.DemoClient.ClientId, default);
        Assert.NotNull(client);

        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Items[Server.RealmCurrentKey] = realm;
        var raw = new NameValueCollection
        {
            { Oidc.Authorize.Request.Prompt, Oidc.PromptModes.None },
            { Oidc.Authorize.Request.Scope, Server.StandardScopes.OpenId },
        };
        var context = new AuthorizeContext(httpContext, raw);
        context.Load(NullLogger.Instance);
        context.ClientParameters.SetClient(client);
        context.RedirectUri = "http://localhost:5000/callback";
        context.RedirectUriValidated();

        var decorator = scope.ServiceProvider.GetRequiredService<PromptNoneInteractionDecorator>();
        await decorator.Decorate(
            context,
            () =>
            {
                // Models a terminal component appended through CustomizeAuthorizeContext: it deliberately
                // produces UI and does not invoke another continuation.
                context.Response = new InteractionResponse(context)
                {
                    RedirectUrl = "/custom-interaction",
                };
                return Task.CompletedTask;
            },
            default);

        var response = Assert.IsType<AuthorizeErrorResponse>(context.Response);
        Assert.Equal(Oidc.Authorize.Errors.InteractionRequired, response.Error);
        Assert.Equal("The request requires user interaction.", response.ErrorDescription);
    }

    [Fact]
    public async Task NewAuthenticatedSession_ChangesSessionState_AndPromptNoneStillSucceeds()
    {
        factory.ResetClock();
        var client = CreateClient();
        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Alice);
        var first = AssertCallback(await client.GetAsync(BuildAuthorizeUrl()))["session_state"];

        await client.LoginAsync(factory.Handles.Demo, factory.Handles.Bob);
        var changed = AssertCallback(await client.GetAsync(BuildAuthorizeUrl(("prompt", "none"))));

        Assert.NotNull(first);
        Assert.NotNull(changed["code"]);
        Assert.False(string.IsNullOrEmpty(changed["session_state"]));
        Assert.NotEqual(first, changed["session_state"]);
        Assert.Null(changed["error"]);
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private string BuildAuthorizeUrl(params (string Key, string Value)[] extra)
        => BuildAuthorizeUrl(factory.Handles.DemoClient.ClientId, "openid profile", extra);

    private string BuildAuthorizeUrl(string scope, params (string Key, string Value)[] extra)
        => BuildAuthorizeUrl(factory.Handles.DemoClient.ClientId, scope, extra);

    private string BuildAuthorizeUrl(
        string clientId,
        string scope,
        params (string Key, string Value)[] extra)
        => BuildAuthorizeUrlCore(clientId, scope, "http://localhost:5000/callback", extra);

    private string BuildAuthorizeUrlWithBinding(
        string clientId,
        string redirectUri,
        params (string Key, string Value)[] extra)
        => BuildAuthorizeUrlCore(clientId, "openid profile", redirectUri, extra);

    private string BuildAuthorizeUrlCore(
        string clientId,
        string scope,
        string redirectUri,
        (string Key, string Value)[] extra)
    {
        var responseMode = extra
            .FirstOrDefault(parameter => parameter.Key == "response_mode")
            .Value ?? "query";
        var url = Oidc.Routes.BuildAuthorizeUrl(factory.Handles.Demo.Path)
            .AddQueryString("client_id", clientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", responseMode)
            .AddQueryString("scope", scope)
            .AddQueryString("redirect_uri", redirectUri)
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        foreach (var (key, value) in extra)
        {
            if (key == "response_mode")
                continue;

            url = url.AddQueryString(key, value);
        }

        return url;
    }

    private static System.Collections.Specialized.NameValueCollection AssertCallback(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("localhost", location.Host);
        Assert.Equal("/callback", location.AbsolutePath);
        return HttpUtility.ParseQueryString(location.Query);
    }
}
