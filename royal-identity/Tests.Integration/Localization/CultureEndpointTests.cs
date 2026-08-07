using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using Microsoft.Extensions.DependencyInjection;
using Tests.Integration.Prepare;

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

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<HttpResponseMessage> PostAsync(
        string locale,
        string returnUrl,
        HttpClient? client = null)
    {
        client ??= CreateClient();

        return await client.PostAsync(
            $"/{factory.Handles.Demo.Path}/account/culture",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["locale"] = locale,
                ["returnUrl"] = returnUrl,
            }));
    }
}
