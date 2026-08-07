using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts.Localization;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Localization;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contexts;
using RoyalIdentity.Users.Contracts;
using Tests.Integration.Prepare;

namespace Tests.Integration.Localization;

/// <summary>
/// Fase 3 (plan-localization.md) — culture selection follows the realm's own policy in the DF5 order, and
/// every hint is a preference that can be ignored without ever failing the request (DF6/DF20).
/// </summary>
public class RequestCultureTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public RequestCultureTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    // Exact match wins.
    [InlineData("pt-BR", new[] { "en", "pt-BR", "es-419" }, "pt-BR")]
    [InlineData("es-419", new[] { "en", "pt-BR", "es-419" }, "es-419")]
    // Casing is normalized, not rejected.
    [InlineData("PT-br", new[] { "en", "pt-BR", "es-419" }, "pt-BR")]
    // Parent culture: pt-BR is offered, the request asked for the parent's other child.
    [InlineData("pt", new[] { "en", "pt" }, "pt")]
    // DF20: a single offered variant of the same language is an acceptable answer for es-MX.
    [InlineData("es-MX", new[] { "en", "es-419" }, "es-419")]
    // ...but two candidates are ambiguous, and inventing one is worse than falling through.
    [InlineData("es-MX", new[] { "en", "es-419", "es-ES" }, null)]
    // Unknown, malformed and non-tag inputs are simply not matches.
    [InlineData("zz-ZZ", new[] { "en", "pt-BR" }, null)]
    [InlineData("not a tag", new[] { "en", "pt-BR" }, null)]
    [InlineData("en_US", new[] { "en", "pt-BR" }, null)]
    [InlineData("", new[] { "en", "pt-BR" }, null)]
    [InlineData(null, new[] { "en", "pt-BR" }, null)]
    public void LocaleMatcher_ResolvesOnlyWhatTheRealmOffers(
        string? requested,
        string[] supported,
        string? expected)
    {
        var matched = LocaleMatcher.TryMatch(requested, supported, out var result);

        Assert.Equal(expected is not null, matched);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("fr pt-BR en", "pt-BR")]
    [InlineData("zz-ZZ es-419", "es-419")]
    [InlineData("fr de", null)]
    public void LocaleMatcher_HonoursThePreferenceOrderOfTheList(string list, string? expected)
    {
        LocaleMatcher.TryMatchPreferenceList(list, ["en", "pt-BR", "es-419"], out var result);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task AcceptLanguage_SelectsASupportedCultureForTheResponse()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pt-BR"));

        var response = await client.GetAsync(LoginUrl());

        Assert.Equal("pt-BR", ContentLanguage(response));
    }

    [Fact]
    public async Task AcceptLanguage_ThatTheRealmDoesNotOffer_FallsBackToTheRealmDefault()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("fr-FR"));

        var response = await client.GetAsync(LoginUrl());

        Assert.Equal("en", ContentLanguage(response));
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task UiLocales_OutranksAcceptLanguage()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pt-BR"));

        var response = await client.GetAsync(
            $"{LoginUrl()}?{Oidc.Authorize.Request.UiLocales}=es-419");

        Assert.Equal("es-419", ContentLanguage(response));
    }

    [Fact]
    public async Task AnUnknownUiLocales_DoesNotFailTheRequestAndFallsThrough()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pt-BR"));

        var response = await client.GetAsync(
            $"{LoginUrl()}?{Oidc.Authorize.Request.UiLocales}=zz-ZZ");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("pt-BR", ContentLanguage(response));
    }

    [Fact]
    public async Task ADisabledRealm_DoesNotNegotiateAndUsesItsDefault()
    {
        await factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.Internationalization.Enabled = false);
        try
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("pt-BR"));

            var response = await client.GetAsync(LoginUrl());

            Assert.Equal("en", ContentLanguage(response));
        }
        finally
        {
            await factory.UpdateRealmAsync(
                factory.Handles.Demo,
                options => options.Internationalization.Enabled = true);
        }
    }

    [Fact]
    public async Task UiLocales_FromStoredAuthorizationParameters_OutranksAcceptLanguage()
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        Assert.True(realm.Options.StoreAuthorizationParameters);

        using var scope = factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
        var verifier = CryptoRandom.CreateUniqueId();
        var challenge = Base64Url.Encode(Encoding.ASCII.GetBytes(verifier).Sha256());
        var parameters = new NameValueCollection
        {
            [Oidc.Authorize.Request.ClientId] = factory.Handles.DemoClient.ClientId,
            [Oidc.Authorize.Request.ResponseType] = Oidc.ResponseTypes.Code,
            [Oidc.Authorize.Request.ResponseMode] = Oidc.ResponseModes.Query,
            [Oidc.Authorize.Request.Scope] = Server.StandardScopes.OpenId,
            [Oidc.Authorize.Request.RedirectUri] = "http://localhost/callback",
            [Oidc.Authorize.Request.State] = "localization-state",
            [Oidc.Authorize.Request.CodeChallenge] = challenge,
            [Oidc.Authorize.Request.CodeChallengeMethod] = Oidc.CodeChallenge.Methods.Sha256,
            [Oidc.Authorize.Request.UiLocales] = "es-419",
        };
        var store = storage.GetAuthorizeParametersStore(realm);
        var handle = await store.WriteAsync(parameters, default);
        var returnUrl = Oidc.Routes.BuildAuthorizeCallbackUrl(realm.Path).EnsureLeadingSlash()!
            .AddQueryString(Oidc.Routes.Params.Authorization, handle);

        var httpContext = RequestContext(scope.ServiceProvider, realm);
        httpContext.Request.QueryString = QueryString.Create(Constants.UI.Routes.Params.ReturnUrl, returnUrl);
        httpContext.Request.Headers.AcceptLanguage = "pt-BR";
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var result = await CreateProvider(scope.ServiceProvider)
            .DetermineProviderCultureResult(httpContext);

        Assert.Equal("es-419", Culture(result));
        // Account screens resolve the same request more than once; culture negotiation must not consume it.
        Assert.NotNull(await store.ReadAsync(handle, default));
    }

    [Fact]
    public async Task ACancelledStoredParameterLookup_FallsThroughToAcceptLanguage()
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        using var rootScope = factory.Services.CreateScope();
        var resolver = new CancelledAuthorizationContextResolver();
        using var services = new ServiceCollection()
            .AddSingleton<IAuthorizationContextResolver>(resolver)
            .BuildServiceProvider();
        var httpContext = RequestContext(services, realm);
        httpContext.Request.QueryString = QueryString.Create(
            Constants.UI.Routes.Params.ReturnUrl,
            $"/{realm.Path}/connect/authorize/callback?authorization=cancelled");
        httpContext.Request.Headers.AcceptLanguage = "pt-BR";
        httpContext.RequestAborted = new CancellationToken(canceled: true);

        var result = await CreateProvider(rootScope.ServiceProvider)
            .DetermineProviderCultureResult(httpContext);

        Assert.True(resolver.WasCalled);
        Assert.Equal("pt-BR", Culture(result));
    }

    [Fact]
    public async Task ARequestWithoutARealm_UsesTheNeutralCatalogueWhenNoLanguageMatches()
    {
        using var scope = factory.Services.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Headers.AcceptLanguage = "fr-FR";

        var result = await CreateProvider(scope.ServiceProvider)
            .DetermineProviderCultureResult(httpContext);

        Assert.Equal("en", Culture(result));
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private string LoginUrl() => $"/{factory.Handles.Demo.Path}/account/login";

    private static string? ContentLanguage(HttpResponseMessage response)
        => response.Content.Headers.ContentLanguage.FirstOrDefault();

    private static RealmRequestCultureProvider CreateProvider(IServiceProvider services)
        => new(services.GetRequiredService<IUiLocaleCatalog>());

    private static DefaultHttpContext RequestContext(
        IServiceProvider services,
        RoyalIdentity.Models.Realm realm)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Get;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localization.test");
        context.Items[Server.RealmCurrentKey] = realm;
        return context;
    }

    private static string Culture(ProviderCultureResult? result)
        => Assert.Single(Assert.IsType<ProviderCultureResult>(result).Cultures).Value!;

    private sealed class CancelledAuthorizationContextResolver : IAuthorizationContextResolver
    {
        public bool WasCalled { get; private set; }

        public Task<AuthorizationContext?> ResolveAsync(string? returnUrl, CancellationToken ct = default)
        {
            WasCalled = true;
            Assert.True(ct.IsCancellationRequested);
            throw new OperationCanceledException(ct);
        }
    }
}
