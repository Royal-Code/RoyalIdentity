using System.Net;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Razor.Localization;
using RoyalIdentity.Contracts.Storage;
using Microsoft.Extensions.DependencyInjection;
using Tests.Integration.Prepare;
using SetCookieHeaderValue = Microsoft.Net.Http.Headers.SetCookieHeaderValue;

namespace Tests.Integration.Localization;

/// <summary>
/// Fase 3 (plan-localization.md) — the explicit-choice endpoint stores only a locale this realm offers and
/// only ever returns to an address inside the realm (DF10), and the logout screens keep the
/// <c>ui_locales</c> carried by their protected message (DF5).
/// </summary>
public class CultureEndpointTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public CultureEndpointTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    // Anything that could leave the site falls back to the realm's own login screen.
    [InlineData("https://attacker.example/steal", "/demo/account/login")]
    [InlineData("//attacker.example/steal", "/demo/account/login")]
    [InlineData("/other-realm/account/login", "/demo/account/login")]
    [InlineData("", "/demo/account/login")]
    // A path inside the realm is preserved.
    [InlineData("/demo/account/profile", "/demo/account/profile")]
    public async Task TheEndpoint_OnlyEverReturnsInsideTheRealm(string requested, string expected)
    {
        var response = await PostAsync("pt-BR", requested);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task TheEndpoint_StoresTheChoiceAsARealmScopedCookie()
    {
        // Asserted on the response rather than on a follow-up request: the cookie is Secure, and the test
        // client speaks http, so it would never send it back.
        var response = await PostAsync("PT-br", "/demo/account/login");

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains($".roid.culture.{factory.Handles.Demo.Path}=pt-BR", setCookie, StringComparison.Ordinal);
        Assert.Contains($"path=/{factory.Handles.Demo.Path}", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheAccountLayout_RendersAnIndependentWorkingSelectorAlongsideThePageForm()
    {
        var client = CreateClient();
        var pagePath = $"/{factory.Handles.Demo.Path}/account/login?source=selector-test";
        var page = await client.GetAsync(pagePath);
        var document = new HtmlDocument();
        document.LoadHtml(await page.Content.ReadAsStringAsync());

        var selector = document.DocumentNode.SelectSingleNode(
            "//form[contains(concat(' ', normalize-space(@class), ' '), ' culture-selector ')]");
        Assert.NotNull(selector);
        Assert.Equal($"/{factory.Handles.Demo.Path}/account/culture", selector.GetAttributeValue("action", ""));
        Assert.Equal(
            ["en", "pt-BR", "es-419"],
            selector.SelectNodes(".//option").Select(option => option.GetAttributeValue("value", "")));
        Assert.Equal(
            pagePath.TrimStart('/'),
            selector.SelectSingleNode(".//input[@name='returnUrl']").GetAttributeValue("value", ""));

        var selectorResponse = await new FormAction(client, selector)
            .SetValue("locale", "pt-BR")
            .SubmitAsync();

        Assert.Equal(HttpStatusCode.Redirect, selectorResponse.StatusCode);
        Assert.Equal(pagePath, selectorResponse.Headers.Location!.ToString());

        // Both tokens came from the same SSR document. Posting the selector must not invalidate or divert the
        // named login form that shares the page.
        var pageForm = document.DocumentNode.SelectSingleNode("//main//form");
        var loginResponse = await new FormAction(client, pageForm)
            .SetValue("Input.Username", "alice")
            .SetValue("Input.Password", "wrong")
            .SubmitAsync();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task TheAccountLayout_DoesNotRenderASelectorWhenTheRealmOffersNoChoice()
    {
        await factory.UpdateRealmAsync(factory.Handles.Demo, options =>
        {
            options.Internationalization.SupportedLocales.Clear();
            options.Internationalization.SupportedLocales.Add("en");
        });
        try
        {
            var response = await CreateClient().GetAsync(
                $"/{factory.Handles.Demo.Path}/account/login");
            var document = new HtmlDocument();
            document.LoadHtml(await response.Content.ReadAsStringAsync());

            Assert.Null(document.DocumentNode.SelectSingleNode(
                "//form[contains(concat(' ', normalize-space(@class), ' '), ' culture-selector ')]"));
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
    public async Task TheEndpoint_RefusesALocaleTheRealmDoesNotOffer()
    {
        var response = await PostAsync("fr", "/demo/account/login");

        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task TheLogoutScreen_UsesTheUiLocalesOfItsProtectedMessage()
    {
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);
        using var scope = factory.Services.CreateScope();
        var messageStore = scope.ServiceProvider.GetRequiredService<IMessageStore>();

        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(new LogoutMessage
        {
            RealmId = realm.Id,
            SessionId = "session-for-culture",
            UiLocales = "es-419",
        }), default);

        var client = CreateClient();
        var response = await client.GetAsync(
            $"/{factory.Handles.Demo.Path}/account/logout?logoutId={logoutId}");

        // Without reading the protected message, a session started in Spanish would say goodbye in English.
        Assert.Equal("es-419", response.Content.Headers.ContentLanguage.FirstOrDefault());
    }

    /// <summary>Mints a genuine antiforgery cookie/token pair from the host's own services.</summary>
    private (SetCookieHeaderValue Cookie, AntiforgeryTokenSet Tokens) MintAntiforgery()
    {
        using var scope = factory.Services.CreateScope();
        var antiforgery = scope.ServiceProvider.GetRequiredService<IAntiforgery>();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        var cookie = SetCookieHeaderValue.Parse(
            httpContext.Response.Headers[HeaderNames.SetCookie].ToString());

        return (cookie, tokens);
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>
    /// Posts with a genuine antiforgery pair. Minting it through <see cref="IAntiforgery"/> and sending the
    /// matching cookie is what makes the other assertions about behaviour rather than about the guard.
    /// </summary>
    private async Task<HttpResponseMessage> PostAsync(
        string locale,
        string returnUrl,
        HttpClient? client = null)
    {
        client ??= CreateClient();

        var (cookie, tokens) = MintAntiforgery();

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{factory.Handles.Demo.Path}/account/culture")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["locale"] = locale,
                ["returnUrl"] = returnUrl,
                [tokens.FormFieldName] = tokens.RequestToken!,
            }),
        };
        request.Headers.Add("Cookie", $"{cookie.Name}={cookie.Value}");

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task TheEndpoint_RejectsAPostWithoutAnAntiforgeryToken()
    {
        // Reading Request.Form by hand gives an endpoint no antiforgery metadata, so the host middleware never
        // covers it: without the explicit validation this POST would change state cross-site.
        var response = await CreateClient().PostAsync(
            $"/{factory.Handles.Demo.Path}/account/culture",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["locale"] = "pt-BR",
                ["returnUrl"] = "/demo/account/login",
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task TheEndpoint_RejectsAPostWhoseTokenBelongsToAnotherCookie()
    {
        // Two genuine mints, then the cookie of one paired with the token of the other. Posting a forged
        // string with no cookie at all would prove far less: it is the pairing that antiforgery checks.
        var (firstCookie, _) = MintAntiforgery();
        var (_, secondToken) = MintAntiforgery();

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{factory.Handles.Demo.Path}/account/culture")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["locale"] = "pt-BR",
                ["returnUrl"] = "/demo/account/login",
                ["__RequestVerificationToken"] = secondToken.RequestToken!,
            }),
        };
        request.Headers.Add("Cookie", $"{firstCookie.Name}={firstCookie.Value}");

        var response = await CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheLogoutScreen_IgnoresAMessageMintedForAnotherRealm()
    {
        // The logout identifier is opaque but not realm-bound, so a message from realm A must not steer the
        // culture of realm B.
        var otherRealm = await factory.LoadRealmAsync(factory.Handles.Server);
        using var scope = factory.Services.CreateScope();
        var messageStore = scope.ServiceProvider.GetRequiredService<IMessageStore>();

        var logoutId = await messageStore.WriteAsync(new Message<LogoutMessage>(new LogoutMessage
        {
            RealmId = otherRealm.Id,
            SessionId = "session-from-another-realm",
            UiLocales = "es-419",
        }), default);

        var response = await CreateClient().GetAsync(
            $"/{factory.Handles.Demo.Path}/account/logout?logoutId={logoutId}");

        Assert.NotEqual("es-419", response.Content.Headers.ContentLanguage.FirstOrDefault());
    }
}
