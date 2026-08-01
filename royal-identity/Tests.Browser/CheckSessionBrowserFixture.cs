using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Tests.Integration.Prepare;

namespace Tests.Browser;

public sealed class CheckSessionBrowserFixture : IAsyncLifetime
{
    private X509Certificate2? certificate;
    private BrowserOpFactory? opFactory;
    private IPlaywright? playwright;

    public const string DemoClientId = "browser_session_client";
    public const string ConsentClientId = "browser_consent_client";
    public const string SecondClientId = "browser_second_realm_client";

    public IBrowser Browser { get; private set; } = null!;

    internal BrowserRpHost PrimaryRp { get; private set; } = null!;

    internal BrowserRpHost AlternateRp { get; private set; } = null!;

    public Uri OpOrigin { get; private set; } = null!;

    public TestRealmHandle DemoRealm => RequireFactory().Handles.Demo;

    public TestRealmHandle SecondRealm { get; private set; } = null!;

    public TestSubjectHandle Alice => RequireFactory().Handles.Alice;

    public TestSubjectHandle Bob => RequireFactory().Handles.Bob;

    public async Task InitializeAsync()
    {
        certificate = EphemeralHttpsCertificate.Create();
        PrimaryRp = await BrowserRpHost.StartAsync(certificate);
        AlternateRp = await BrowserRpHost.StartAsync(certificate);

        opFactory = new BrowserOpFactory();
        opFactory.UseKestrel(options => options.Listen(
            IPAddress.Loopback,
            0,
            listen => listen.UseHttps(certificate)));
        using var startupClient = opFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        var addresses = opFactory.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("The OP did not publish a Kestrel address.");
        OpOrigin = new Uri(Assert.Single(
            addresses,
            value => value.StartsWith("https://", StringComparison.Ordinal)));

        await ConfigureDemoRealmAsync();
        SecondRealm = await ConfigureSecondRealmAsync();

        playwright = await Playwright.CreateAsync();
        Browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    internal string BuildAuthorizeUrl(
        TestRealmHandle realm,
        string clientId,
        BrowserRpHost rp,
        string? prompt = null)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["response_mode"] = "query",
            ["scope"] = "openid profile",
            ["redirect_uri"] = new Uri(rp.Origin, "/callback").ToString(),
            ["state"] = "browser-caller-state",
        };
        if (prompt is not null)
            parameters["prompt"] = prompt;

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            new Uri(OpOrigin, $"/{realm.Path}/connect/authorize").ToString(),
            parameters);
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.DisposeAsync();
        playwright?.Dispose();
        if (opFactory is not null)
            await opFactory.DisposeAsync();
        if (AlternateRp is not null)
            await AlternateRp.DisposeAsync();
        if (PrimaryRp is not null)
            await PrimaryRp.DisposeAsync();
        certificate?.Dispose();
    }

    private async Task ConfigureDemoRealmAsync()
    {
        var factory = RequireFactory();
        await factory.UpdateRealmAsync(factory.Handles.Demo, options =>
        {
            options.Endpoints.EnableCheckSessionEndpoint = true;
            options.Authentication.CheckSessionCookieName = ".roid.browser-demo";
        });
        await SaveClientAsync(factory.Handles.Demo, DemoClientId, requireConsent: false);
        await SaveClientAsync(factory.Handles.Demo, ConsentClientId, requireConsent: true);
    }

    private async Task<TestRealmHandle> ConfigureSecondRealmAsync()
    {
        var factory = RequireFactory();
        var handle = factory.Handles.Account;
        await factory.UpdateRealmAsync(handle, options =>
        {
            options.Endpoints.EnableCheckSessionEndpoint = true;
            options.Authentication.CheckSessionCookieName = ".roid.browser-second";
        });
        await SaveClientAsync(handle, SecondClientId, requireConsent: false);
        return handle;
    }

    private Task SaveClientAsync(TestRealmHandle realm, string clientId, bool requireConsent)
        => RequireFactory().SaveClientAsync(realm, clientId, configured =>
        {
            configured.Name = clientId;
            configured.RequireClientSecret = false;
            configured.RequirePkce = false;
            configured.RequireConsent = requireConsent;
            configured.AllowRememberConsent = false;
            configured.AllowedGrantTypes.Add("authorization_code");
            configured.AllowedResponseTypes.Add("code");
            configured.AllowedIdentityScopes.UnionWith(["openid", "profile"]);
            configured.RedirectUris.Add(new Uri(PrimaryRp.Origin, "/callback").ToString());
            configured.RedirectUris.Add(new Uri(AlternateRp.Origin, "/callback").ToString());
        });

    private BrowserOpFactory RequireFactory()
        => opFactory ?? throw new InvalidOperationException("The browser fixture is not initialized.");

    private sealed class BrowserOpFactory : PersistentStorageAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureLogging(logging => logging.ClearProviders());
        }
    }
}
