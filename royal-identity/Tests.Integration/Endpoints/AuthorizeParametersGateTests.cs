using System.Collections.Specialized;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Endpoints;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Responses.HttpResults;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// The <c>StoreAuthorizationParameters</c> gate at flow level (plan-data-operational-storage Fase 6, MP-5/DF16).
/// With it on, the authorize parameters live server-side and only a handle travels in the URL; with it off,
/// nothing in the login/callback path may touch the store — not even when a handle is present in the query.
/// </summary>
public class AuthorizeParametersGateTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public AuthorizeParametersGateTests(AppFactory factory) => this.factory = factory;

    // With the option on (the product default), the login redirect carries only the handle.
    [Fact]
    public async Task WithTheOptionOn_TheLoginRedirect_CarriesOnlyTheHandle()
    {
        var realm = await CreateGatedRealmAsync(storeAuthorizationParameters: true);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(AuthorizeUrl(realm.Path));

        var parameters = ReturnUrlOf(realm, response).ReadQueryStringAsNameValueCollection();
        var handle = parameters[Oidc.Routes.Params.Authorization];

        Assert.NotNull(handle);
        Assert.Null(parameters["client_id"]);
        Assert.Null(parameters["redirect_uri"]);

        // The handle resolves to the parameters that were kept server-side.
        var stored = await factory.Services.GetRequiredService<IStorage>()
            .GetAuthorizeParametersStore(realm)
            .ReadAsync(handle, default);

        Assert.NotNull(stored);
        Assert.Equal("gate_client", stored["client_id"]);
    }

    // With the option off, the parameters travel in the query string and no handle is issued.
    [Fact]
    public async Task WithTheOptionOff_TheLoginRedirect_CarriesTheRawParameters()
    {
        var realm = await CreateGatedRealmAsync(storeAuthorizationParameters: false);
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(AuthorizeUrl(realm.Path));

        var parameters = ReturnUrlOf(realm, response).ReadQueryStringAsNameValueCollection();

        Assert.Null(parameters[Oidc.Routes.Params.Authorization]);
        Assert.Equal("gate_client", parameters["client_id"]);
        Assert.Equal("code", parameters["response_type"]);
        Assert.Equal(RedirectUri, parameters["redirect_uri"]);
    }

    // The callback is the read side of the same gate: with the option off it uses the query it was given, even
    // when a handle is sitting in it.
    [Fact]
    public async Task WithTheOptionOff_TheCallback_IgnoresAStaleHandle_AndUsesTheQuery()
    {
        var realm = await CreateGatedRealmAsync(storeAuthorizationParameters: false);
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        // A record that exists and would win if the gate were not honored.
        var staleHandle = await storage.GetAuthorizeParametersStore(realm).WriteAsync(
            new NameValueCollection { ["client_id"] = "stored_client" }, default);

        var httpContext = CallbackHttpContext(
            scope,
            realm,
            $"?client_id=gate_client&response_type=code&{Oidc.Routes.Params.Authorization}={staleHandle}");

        var result = await scope.ServiceProvider
            .GetRequiredService<AuthorizeCallbackEndpoint>()
            .TryCreateContextAsync(httpContext);

        Assert.True(result.IsValid(out var context, out _));
        Assert.Equal("gate_client", ((AuthorizeContext)context).Raw["client_id"]);
        // The stored record was neither read nor consumed.
        Assert.NotNull(await storage.GetAuthorizeParametersStore(realm).ReadAsync(staleHandle, default));
    }

    // With the option on the callback consumes the record: it reads the parameters and deletes the handle.
    [Fact]
    public async Task WithTheOptionOn_TheCallback_ReadsAndConsumesTheHandle()
    {
        var realm = await CreateGatedRealmAsync(storeAuthorizationParameters: true);
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        var handle = await storage.GetAuthorizeParametersStore(realm).WriteAsync(
            new NameValueCollection { ["client_id"] = "stored_client", ["response_type"] = "code" }, default);

        var httpContext = CallbackHttpContext(
            scope,
            realm,
            $"?client_id=query_client&{Oidc.Routes.Params.Authorization}={handle}");

        var result = await scope.ServiceProvider
            .GetRequiredService<AuthorizeCallbackEndpoint>()
            .TryCreateContextAsync(httpContext);

        Assert.True(result.IsValid(out var context, out _));
        Assert.Equal("stored_client", ((AuthorizeContext)context).Raw["client_id"]);
        Assert.Null(await storage.GetAuthorizeParametersStore(realm).ReadAsync(handle, default));
    }

    // The login/consent screens resolve their context from the returnUrl. With the option off the handle in it
    // is inert: the raw query is authoritative.
    [Fact]
    public async Task WithTheOptionOff_TheResolver_IgnoresAHandleInTheReturnUrl()
    {
        var realm = await CreateGatedRealmAsync(storeAuthorizationParameters: false);
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        // Points at a client that does not exist: resolving through it would fail loudly.
        var staleHandle = await storage.GetAuthorizeParametersStore(realm).WriteAsync(
            new NameValueCollection { ["client_id"] = "absent_client" }, default);

        var returnUrl = CallbackReturnUrl(realm)
            .AddQueryString(Oidc.Routes.Params.Authorization, staleHandle);

        var context = await ResolveAsync(scope, realm, returnUrl);

        Assert.NotNull(context);
        Assert.Equal("gate_client", context.Client.Id);
        Assert.NotNull(await storage.GetAuthorizeParametersStore(realm).ReadAsync(staleHandle, default));
    }

    // With the option on the resolver reads the parameters back from the store — and does not consume them,
    // because the screens resolve more than once before the callback deletes the record.
    [Fact]
    public async Task WithTheOptionOn_TheResolver_ReadsTheParametersFromTheStore()
    {
        var realm = await CreateGatedRealmAsync(storeAuthorizationParameters: true);
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        var handle = await storage.GetAuthorizeParametersStore(realm).WriteAsync(
            CallbackReturnUrl(realm).ReadQueryStringAsNameValueCollection(), default);

        var returnUrl = Oidc.Routes.BuildAuthorizeCallbackUrl(realm.Path).EnsureLeadingSlash()
            .AddQueryString(Oidc.Routes.Params.Authorization, handle);

        var context = await ResolveAsync(scope, realm, returnUrl);

        Assert.NotNull(context);
        Assert.Equal("gate_client", context.Client.Id);
        Assert.NotNull(await storage.GetAuthorizeParametersStore(realm).ReadAsync(handle, default));
    }

    // The consent screen has the same gate as login, and the critério da fase names it explicitly.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConsentPageResult_HonoursTheGate(bool storeAuthorizationParameters)
    {
        var realm = await CreateGatedRealmAsync(storeAuthorizationParameters);
        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();

        var httpContext = CallbackHttpContext(scope, realm, "?client_id=gate_client&response_type=code");
        var context = new AuthorizeContext(httpContext, httpContext.Request.Query.AsNameValueCollection(), null!);

        await new ConsentPageResult(context).ExecuteAsync(httpContext);

        var returnUrl = ReturnUrlOf(realm.Options.UI.ConsentParameter, httpContext);
        var parameters = returnUrl.ReadQueryStringAsNameValueCollection();
        var handle = parameters[Oidc.Routes.Params.Authorization];

        if (storeAuthorizationParameters)
        {
            Assert.NotNull(handle);
            Assert.Null(parameters["client_id"]);
            Assert.Equal(
                "gate_client",
                (await storage.GetAuthorizeParametersStore(realm).ReadAsync(handle, default))!["client_id"]);
        }
        else
        {
            Assert.Null(handle);
            Assert.Equal("gate_client", parameters["client_id"]);
            Assert.Equal("code", parameters["response_type"]);
        }
    }

    private const string RedirectUri = "https://gate.example/callback";

    /// <summary>Creates a realm with the gate in the requested position and a client able to reach the login page.</summary>
    private async Task<RoyalIdentity.Models.Realm> CreateGatedRealmAsync(bool storeAuthorizationParameters)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var path = $"ap-gate-{CryptoRandom.CreateUniqueId(6)}";
        var realm = await manager.CreateAsync(path, $"{path}.test", $"AP Gate Realm {path}");

        realm.Options.StoreAuthorizationParameters = storeAuthorizationParameters;
        await scope.ServiceProvider.GetRequiredService<IStorage>().Realms.SaveAsync(realm);

        var memory = factory.Services.GetRequiredService<MemoryStorage>();
        memory.GetRealmMemoryStore(realm).Clients["gate_client"] = new Client
        {
            Realm = realm,
            Id = "gate_client",
            Name = "AP Gate Client",
            RequireClientSecret = false,
            AllowedGrantTypes = ["authorization_code"],
            AllowedIdentityScopes = { "openid" },
            AllowedResponseTypes = { "code" },
            RedirectUris = { RedirectUri },
        };

        return realm;
    }

    private static string AuthorizeUrl(string realmPath)
    {
        var codeVerifier = CryptoRandom.CreateUniqueId();
        var codeChallenge = Base64Url.Encode(Encoding.ASCII.GetBytes(codeVerifier).Sha256());

        return Oidc.Routes.BuildAuthorizeUrl(realmPath)
            .AddQueryString("client_id", "gate_client")
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", "openid")
            .AddQueryString("redirect_uri", RedirectUri)
            .AddQueryString("state", "gate-state")
            .AddQueryString("code_challenge", codeChallenge)
            .AddQueryString("code_challenge_method", "S256");
    }

    /// <summary>The login redirect always carries the callback URL in the realm's configured login parameter.</summary>
    private static string ReturnUrlOf(RoyalIdentity.Models.Realm realm, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);

        var location = response.Headers.Location!.ToString();
        var returnUrl = location.ReadQueryStringAsNameValueCollection()[realm.Options.UI.LoginParameter];

        Assert.NotNull(returnUrl);
        return returnUrl;
    }

    /// <summary>The callback returnUrl carrying the full authorize request in the query, as the option-off path builds it.</summary>
    private static string CallbackReturnUrl(RoyalIdentity.Models.Realm realm)
    {
        var authorizeUrl = AuthorizeUrl(realm.Path);

        return Oidc.Routes.BuildAuthorizeCallbackUrl(realm.Path).EnsureLeadingSlash()
            + authorizeUrl[authorizeUrl.IndexOf('?')..];
    }

    private static async Task<RoyalIdentity.Users.Contexts.AuthorizationContext?> ResolveAsync(
        IServiceScope scope, RoyalIdentity.Models.Realm realm, string returnUrl)
    {
        var httpContext = CallbackHttpContext(scope, realm, string.Empty);
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        return await scope.ServiceProvider
            .GetRequiredService<RoyalIdentity.Users.Contracts.IAuthorizationContextResolver>()
            .ResolveAsync(returnUrl);
    }

    /// <summary>The screen redirect carries the callback URL in the realm's configured screen parameter.</summary>
    private static string ReturnUrlOf(string parameterName, HttpContext httpContext)
    {
        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);

        var location = httpContext.Response.Headers.Location.ToString();
        var returnUrl = location.ReadQueryStringAsNameValueCollection()[parameterName];

        Assert.NotNull(returnUrl);
        return returnUrl;
    }

    private static DefaultHttpContext CallbackHttpContext(
        IServiceScope scope, RoyalIdentity.Models.Realm realm, string queryString)
    {
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("gate.contract.test");
        httpContext.Request.QueryString = new QueryString(queryString);
        httpContext.Items[Server.RealmCurrentKey] = realm;

        return httpContext;
    }
}
