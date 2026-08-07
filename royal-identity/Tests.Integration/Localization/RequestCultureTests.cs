using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using RoyalIdentity.Localization;
using RoyalIdentity.Options;
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

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private string LoginUrl() => $"/{factory.Handles.Demo.Path}/account/login";

    private static string? ContentLanguage(HttpResponseMessage response)
        => response.Content.Headers.ContentLanguage.FirstOrDefault();
}
