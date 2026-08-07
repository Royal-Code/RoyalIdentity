using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using RoyalIdentity.Localization;
using RoyalIdentity.Options;
using Tests.Integration.Prepare;
using SetCookieHeaderValue = Microsoft.Net.Http.Headers.SetCookieHeaderValue;

namespace Tests.Integration.Localization;

/// <summary>
/// Fase 3 (plan-localization.md) — an explicit language choice is realm-scoped, stored canonically, outranks
/// every hint, and stops being honoured as soon as the realm no longer offers it (DF5/DF10).
/// </summary>
public class CulturePreferenceTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public CulturePreferenceTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AStoredPreference_OutranksUiLocalesAndAcceptLanguage()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(
            new System.Net.Http.Headers.StringWithQualityHeaderValue("en"));
        AddPreference(client, factory.Handles.Demo.Path, "pt-BR");

        var response = await client.GetAsync(
            $"{LoginUrl(factory.Handles.Demo.Path)}?{Oidc.Authorize.Request.UiLocales}=es-419");

        Assert.Equal("pt-BR", ContentLanguage(response));
    }

    [Fact]
    public async Task APreferenceForALocaleTheRealmNoLongerOffers_IsIgnored()
    {
        await factory.UpdateRealmAsync(factory.Handles.Demo, options =>
        {
            options.Internationalization.SupportedLocales.Clear();
            options.Internationalization.SupportedLocales.AddRange(["en", "pt-BR"]);
        });
        try
        {
            var client = CreateClient();
            AddPreference(client, factory.Handles.Demo.Path, "es-419");

            var response = await client.GetAsync(LoginUrl(factory.Handles.Demo.Path));

            // The stale cookie is dropped rather than honoured or rejected: resolution simply continues.
            Assert.Equal("en", ContentLanguage(response));
            Assert.True(response.IsSuccessStatusCode);
        }
        finally
        {
            await factory.UpdateRealmAsync(factory.Handles.Demo, options =>
            {
                options.Internationalization.SupportedLocales.Clear();
                options.Internationalization.SupportedLocales.AddRange(["en", "pt-BR", "es-419"]);
            });
        }
    }

    [Fact]
    public async Task TwoRealmsInTheSameBrowser_DoNotShareAPreference()
    {
        var client = CreateClient();
        AddPreference(client, factory.Handles.Demo.Path, "pt-BR");
        AddPreference(client, factory.Handles.Server.Path, "es-419");

        var demo = await client.GetAsync(LoginUrl(factory.Handles.Demo.Path));
        var server = await client.GetAsync(LoginUrl(factory.Handles.Server.Path));

        Assert.Equal("pt-BR", ContentLanguage(demo));
        Assert.Equal("es-419", ContentLanguage(server));
    }

    [Fact]
    public async Task TheCookieIsRealmScopedEssentialAndNotReadableByScript()
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        using var scope = factory.Services.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Items[Server.RealmCurrentKey] = realm;
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = httpContext;

        var applied = scope.ServiceProvider.GetRequiredService<ICulturePreferenceService>().Apply("PT-br");

        // Only the canonical tag is stored, never the caller's spelling.
        Assert.Equal("pt-BR", applied);
        var setCookie = SetCookieHeaderValue.Parse(
            httpContext.Response.Headers[HeaderNames.SetCookie].ToString());
        Assert.Equal($".roid.culture.{realm.Path}", setCookie.Name.Value);
        Assert.Equal("pt-BR", setCookie.Value.Value);
        Assert.Equal($"/{realm.Path}", setCookie.Path.Value);
        Assert.True(setCookie.HttpOnly);
        Assert.True(setCookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, setCookie.SameSite);
        Assert.NotNull(setCookie.Expires);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("zz-ZZ")]
    [InlineData("en_US")]
    [InlineData("")]
    [InlineData(null)]
    public async Task AChoiceTheRealmDoesNotOffer_IsRefusedWithoutWritingACookie(string? locale)
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        using var scope = factory.Services.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Items[Server.RealmCurrentKey] = realm;
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var applied = scope.ServiceProvider.GetRequiredService<ICulturePreferenceService>().Apply(locale);

        Assert.Null(applied);
        Assert.Empty(httpContext.Response.Headers[HeaderNames.SetCookie].ToString());
    }

    [Fact]
    public async Task TheSelector_OffersTheRealmLocalesInConfiguredOrder()
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        using var scope = factory.Services.CreateScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Items[Server.RealmCurrentKey] = realm;
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var selectable = scope.ServiceProvider.GetRequiredService<ICulturePreferenceService>()
            .GetSelectableLocales();

        Assert.Equal(["en", "pt-BR", "es-419"], selectable);
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static void AddPreference(HttpClient client, string realmPath, string locale)
        => client.DefaultRequestHeaders.Add("Cookie", $".roid.culture.{realmPath}={locale}");

    private static string LoginUrl(string realmPath) => $"/{realmPath}/account/login";

    private static string? ContentLanguage(HttpResponseMessage response)
        => response.Content.Headers.ContentLanguage.FirstOrDefault();
}
