using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.Configuration;
using RoyalIdentity.Contracts.Localization;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

public class LocalizationDiscoveryTests : IClassFixture<PersistentStorageAppFactory>
{
    private readonly PersistentStorageAppFactory factory;

    public LocalizationDiscoveryTests(PersistentStorageAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Get_WithTheComposedUi_MustPublishTheExactProductLocalesAndNoClaimsLocales()
    {
        using var document = await GetDiscoveryAsync(factory, factory.Handles.Demo.Path);
        var root = document.RootElement;

        Assert.Equal(
            ["en", "pt-BR", "es-419"],
            ReadLocales(root));
        Assert.False(root.TryGetProperty(Oidc.Discovery.ClaimsLocalesSupported, out _));
    }

    [Fact]
    public async Task Get_ForAdminRealm_MustDescribeTheGenericOidcUiThatTheHostComposes()
    {
        using var document = await GetDiscoveryAsync(factory, factory.Handles.Admin.Path);

        Assert.Equal(
            ["en", "pt-BR", "es-419"],
            ReadLocales(document.RootElement));
    }

    [Fact]
    public async Task Get_WithTheEmptyHostCatalog_MustOmitLocalesEvenForTheAdminRealm()
    {
        using var emptyCatalogFactory = new EmptyUiCatalogAppFactory();
        using var document = await GetDiscoveryAsync(
            emptyCatalogFactory,
            emptyCatalogFactory.Handles.Admin.Path);

        Assert.False(document.RootElement.TryGetProperty(Oidc.Discovery.UILocalesSupported, out _));
        Assert.False(document.RootElement.TryGetProperty(Oidc.Discovery.ClaimsLocalesSupported, out _));
    }

    [Fact]
    public async Task Get_WithLocalizationDisabled_MustOmitLocales()
    {
        await factory.UpdateRealmAsync(
            factory.Handles.Demo,
            options => options.Internationalization.Enabled = false);
        try
        {
            using var document = await GetDiscoveryAsync(factory, factory.Handles.Demo.Path);

            Assert.False(document.RootElement.TryGetProperty(Oidc.Discovery.UILocalesSupported, out _));
        }
        finally
        {
            await factory.UpdateRealmAsync(
                factory.Handles.Demo,
                options => options.Internationalization.Enabled = true);
        }
    }

    [Fact]
    public async Task Get_MustPutTheDefaultFirstAndPreserveTheConfiguredOrderOfTheOthers()
    {
        await ConfigureLocalesAsync(factory.Handles.Demo, "pt-BR", "es-419", "en", "pt-BR");
        try
        {
            using var document = await GetDiscoveryAsync(factory, factory.Handles.Demo.Path);

            Assert.Equal(
                ["pt-BR", "es-419", "en"],
                ReadLocales(document.RootElement));
        }
        finally
        {
            await RestoreDefaultLocalesAsync(factory.Handles.Demo);
        }
    }

    [Fact]
    public async Task Get_ForTwoRealms_MustKeepTheirPoliciesIsolated()
    {
        await ConfigureLocalesAsync(factory.Handles.Demo, "es-419", "pt-BR", "es-419");
        try
        {
            await ConfigureLocalesAsync(factory.Handles.Server, "pt-BR", "en", "pt-BR");
            using var demo = await GetDiscoveryAsync(factory, factory.Handles.Demo.Path);
            using var server = await GetDiscoveryAsync(factory, factory.Handles.Server.Path);

            Assert.Equal(["es-419", "pt-BR"], ReadLocales(demo.RootElement));
            Assert.Equal(["pt-BR", "en"], ReadLocales(server.RootElement));
        }
        finally
        {
            await RestoreDefaultLocalesAsync(factory.Handles.Demo);
            await RestoreDefaultLocalesAsync(factory.Handles.Server);
        }
    }

    /// <summary>
    /// A locale the composed UI cannot render never reaches the discovery document.
    /// </summary>
    /// <remarks>
    /// Two different guarantees are asserted, and the distinction matters. The snapshot validator (DF8)
    /// refuses to publish the change, so <c>TryRefreshAsync</c> fails and the published snapshot keeps its
    /// previous value — asserted below. That guarantee does not protect request handling, which does
    /// <b>not</b> read the snapshot:
    /// <c>SetCurrentRealmAsync</c> resolves the realm straight from <c>IStorage</c>, so the saved realm is live
    /// for requests regardless. What actually keeps the unsupported locale out of the metadata is the
    /// catalogue filter in <c>DiscoveryHandler</c>, which is therefore load-bearing rather than defensive.
    /// </remarks>
    [Fact]
    public async Task ARealmLocaleTheUiCannotRender_IsRejectedByTheSnapshotAndOmittedFromMetadata()
    {
        using var before = await GetDiscoveryAsync(factory, factory.Handles.Demo.Path);
        var expected = ReadLocales(before.RootElement);
        var realm = await factory.LoadRealmAsync(factory.Handles.Demo);

        realm.Options.Internationalization.SupportedLocales.Add("fr-FR");
        try
        {
            await factory.WithStorageValueAsync(storage => storage.Realms.SaveAsync(realm));
            using var scope = factory.Services.CreateScope();
            var refresher = scope.ServiceProvider.GetRequiredService<IConfigurationSnapshotRefresher>();

            Assert.False(await refresher.TryRefreshAsync());

            // Guarantee 1 — the snapshot was not replaced, even though the authoritative store changed.
            var stored = await factory.LoadRealmAsync(factory.Handles.Demo);
            Assert.Contains("fr-FR", stored.Options.Internationalization.SupportedLocales);
            var published = scope.ServiceProvider
                .GetRequiredService<IConfigurationSnapshot>()
                .FindRealmByPath(factory.Handles.Demo.Path)!;
            Assert.DoesNotContain(
                "fr-FR",
                published.Options.Internationalization.SupportedLocales);

            // Guarantee 2 — the request reads that stored realm, so only the catalogue filter prevents the
            // unsupported locale from being published.
            using var after = await GetDiscoveryAsync(factory, factory.Handles.Demo.Path);
            Assert.Equal(expected, ReadLocales(after.RootElement));
        }
        finally
        {
            realm.Options.Internationalization.SupportedLocales.Remove("fr-FR");
            await factory.SaveRealmAsync(realm);
        }
    }

    private async Task ConfigureLocalesAsync(
        TestRealmHandle realm,
        string defaultLocale,
        params string[] supportedLocales)
    {
        await factory.UpdateRealmAsync(
            realm,
            options =>
            {
                options.Internationalization.Enabled = true;
                options.Internationalization.DefaultLocale = defaultLocale;
                options.Internationalization.SupportedLocales.Clear();
                options.Internationalization.SupportedLocales.AddRange(supportedLocales);
            });
    }

    private Task RestoreDefaultLocalesAsync(TestRealmHandle realm)
        => ConfigureLocalesAsync(realm, "en", "en", "pt-BR", "es-419");

    private static async Task<JsonDocument> GetDiscoveryAsync(
        PersistentStorageAppFactory appFactory,
        string realmPath)
    {
        var client = appFactory.CreateClient();
        var url = Oidc.Routes.BuildDiscoveryConfigurationUrl(realmPath);
        return JsonDocument.Parse(await client.GetStringAsync(url));
    }

    private static string[] ReadLocales(JsonElement root)
        => root.GetProperty(Oidc.Discovery.UILocalesSupported)
            .EnumerateArray()
            .Select(locale => locale.GetString()
                ?? throw new JsonException("Discovery returned a null UI locale."))
            .ToArray();

    private sealed class EmptyUiCatalogAppFactory : PersistentStorageAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUiLocaleCatalog>();
                services.AddSingleton<IUiLocaleCatalog, EmptyUiLocaleCatalog>();
            });
        }
    }
}
