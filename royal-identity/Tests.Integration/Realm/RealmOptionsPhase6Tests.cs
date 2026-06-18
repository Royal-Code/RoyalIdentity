using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Realm;

public class RealmOptionsPhase6Tests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public RealmOptionsPhase6Tests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task RealmOptions_CopyOnCreate_DoesNotSharePromotedOptions()
    {
        var realmA = await CreateRealmAsync("phase6-copy-a");
        var realmB = await CreateRealmAsync("phase6-copy-b");
        var storage = factory.Services.GetRequiredService<IStorage>();

        realmA.Options.Authentication.CookieName = ".phase6";
        realmA.Options.Csp.AddDeprecatedHeader = false;
        realmA.Options.Cors.Enabled = true;
        realmA.Options.Cors.AllowedOrigins.Add("https://phase6.example");
        realmA.Options.Logging.SensitiveValuesFilter.Add("phase6-secret");
        realmA.Options.InputLengthRestrictions.ClientId = 42;
        realmA.Options.AccessTokenJwtType = "phase6+jwt";
        realmA.Options.EmitScopesAsSpaceDelimitedStringInJwt = true;
        realmA.Options.DispatchEvents = true;
        await storage.Realms.SaveAsync(realmA);

        Assert.Equal(Constants.Server.DefaultCookieName, realmB.Options.Authentication.CookieName);
        Assert.True(realmB.Options.Csp.AddDeprecatedHeader);
        Assert.False(realmB.Options.Cors.Enabled);
        Assert.DoesNotContain("https://phase6.example", realmB.Options.Cors.AllowedOrigins);
        Assert.DoesNotContain("phase6-secret", realmB.Options.Logging.SensitiveValuesFilter);
        Assert.Equal(100, realmB.Options.InputLengthRestrictions.ClientId);
        Assert.Equal("at+jwt", realmB.Options.AccessTokenJwtType);
        Assert.False(realmB.Options.EmitScopesAsSpaceDelimitedStringInJwt);
        Assert.False(realmB.Options.DispatchEvents);

        Assert.Equal(Constants.Server.DefaultCookieName, storage.ServerOptions.Authentication.CookieName);
        Assert.True(storage.ServerOptions.Csp.AddDeprecatedHeader);
        Assert.False(storage.ServerOptions.Cors.Enabled);
        Assert.DoesNotContain("https://phase6.example", storage.ServerOptions.Cors.AllowedOrigins);
        Assert.DoesNotContain("phase6-secret", storage.ServerOptions.Logging.SensitiveValuesFilter);
        Assert.Equal(100, storage.ServerOptions.InputLengthRestrictions.ClientId);
        Assert.Equal("at+jwt", storage.ServerOptions.AccessTokenJwtType);
        Assert.False(storage.ServerOptions.EmitScopesAsSpaceDelimitedStringInJwt);
        Assert.False(storage.ServerOptions.DispatchEvents);

        Assert.NotSame(realmA.Options.Authentication, realmB.Options.Authentication);
        Assert.NotSame(realmA.Options.Csp, realmB.Options.Csp);
        Assert.NotSame(realmA.Options.Cors, realmB.Options.Cors);
        Assert.NotSame(realmA.Options.Cors.AllowedOrigins, realmB.Options.Cors.AllowedOrigins);
        Assert.NotSame(realmA.Options.Logging, realmB.Options.Logging);
        Assert.NotSame(realmA.Options.Logging.SensitiveValuesFilter, realmB.Options.Logging.SensitiveValuesFilter);
        Assert.NotSame(realmA.Options.InputLengthRestrictions, realmB.Options.InputLengthRestrictions);
        Assert.NotSame(storage.ServerOptions.Cors, realmA.Options.Cors);
        Assert.NotSame(storage.ServerOptions.Cors.AllowedOrigins, realmA.Options.Cors.AllowedOrigins);
    }

    [Fact]
    public void RealmOptions_CopyFromServer_PropagatesConfiguredValues()
    {
        var serverOptions = CreateConfiguredServerOptions();

        var realmOptions = new RealmOptions(serverOptions);

        Assert.Equal(".server-copy", realmOptions.Authentication.CookieName);
        Assert.Equal(TimeSpan.FromMinutes(17), realmOptions.Authentication.CookieLifetime);
        Assert.Equal(CspLevel.One, realmOptions.Csp.Level);
        Assert.False(realmOptions.Csp.AddDeprecatedHeader);
        Assert.True(realmOptions.Cors.Enabled);
        Assert.True(realmOptions.Cors.AllowCredentials);
        Assert.Contains("https://server-copy.example", realmOptions.Cors.AllowedOrigins);
        Assert.Contains("x-server-copy", realmOptions.Cors.AllowedHeaders);
        Assert.Contains("PATCH", realmOptions.Cors.AllowedMethods);
        Assert.Contains("server-copy-secret", realmOptions.Logging.SensitiveValuesFilter);
        Assert.Equal(31, realmOptions.InputLengthRestrictions.ClientId);
        Assert.False(realmOptions.Discovery.ShowEndpoints);
        Assert.Equal(90, realmOptions.Discovery.ResponseCacheInterval);
        Assert.Equal("value", realmOptions.Discovery.CustomEntries["server_copy"]);
        Assert.Contains("server-prompt", realmOptions.Discovery.SupportedPromptModes);
        Assert.False(realmOptions.Endpoints.EnableTokenEndpoint);
        Assert.True(realmOptions.Endpoints.EnableJwtRequestUri);
        Assert.True(realmOptions.MutualTls.Enabled);
        Assert.Equal("mtls.example", realmOptions.MutualTls.DomainName);
        Assert.True(realmOptions.MutualTls.AlwaysEmitConfirmationClaim);
        Assert.Equal(SecurityAlgorithms.RsaSha512, realmOptions.Keys.MainSigningCredentialsAlgorithm);
        Assert.Equal(TimeSpan.FromDays(12), realmOptions.Keys.DefaultSigningCredentialsLifetime);
        Assert.Equal(4096, realmOptions.Keys.RsaKeySizeInBytes);
        Assert.Contains(SecurityAlgorithms.RsaSha512, realmOptions.Keys.SigningCredentialsAlgorithms);
        Assert.Equal("server-copy+jwt", realmOptions.AccessTokenJwtType);
        Assert.True(realmOptions.EmitScopesAsSpaceDelimitedStringInJwt);
        Assert.True(realmOptions.DispatchEvents);

        Assert.NotSame(serverOptions.Authentication, realmOptions.Authentication);
        Assert.NotSame(serverOptions.Csp, realmOptions.Csp);
        Assert.NotSame(serverOptions.Cors, realmOptions.Cors);
        Assert.NotSame(serverOptions.Cors.AllowedOrigins, realmOptions.Cors.AllowedOrigins);
        Assert.NotSame(serverOptions.Logging, realmOptions.Logging);
        Assert.NotSame(serverOptions.Logging.SensitiveValuesFilter, realmOptions.Logging.SensitiveValuesFilter);
        Assert.NotSame(serverOptions.InputLengthRestrictions, realmOptions.InputLengthRestrictions);
        Assert.NotSame(serverOptions.Discovery, realmOptions.Discovery);
        Assert.NotSame(serverOptions.Discovery.CustomEntries, realmOptions.Discovery.CustomEntries);
        Assert.NotSame(serverOptions.Discovery.SupportedPromptModes, realmOptions.Discovery.SupportedPromptModes);
        Assert.NotSame(serverOptions.Endpoints, realmOptions.Endpoints);
        Assert.NotSame(serverOptions.MutualTls, realmOptions.MutualTls);
        Assert.NotSame(serverOptions.Keys, realmOptions.Keys);
        Assert.NotSame(serverOptions.Keys.SigningCredentialsAlgorithms, realmOptions.Keys.SigningCredentialsAlgorithms);
    }

    [Fact]
    public void RealmOptions_CopyFromRealm_IsIndependent()
    {
        var source = new RealmOptions(CreateConfiguredServerOptions())
        {
            IssuerUri = "https://issuer.example/source",
            LowerCaseIssuerUri = false,
            IncludeRealmPathToIssuerUri = false,
            StoreAuthorizationParameters = false
        };
        source.UI.AccessDeniedPath = "/{realm}/account/source-denied";
        source.Caching.KeyCacheDuration = TimeSpan.FromMinutes(22);
        source.Account.AllowRememberLogin = false;
        source.Account.InvalidCredentialsErrorMessage = "source invalid";
        source.Account.RememberMeLoginDuration = TimeSpan.FromDays(9);
        source.Branding.LogoUri = "https://cdn.example/logo.svg";
        source.Branding.FaviconUri = "https://cdn.example/favicon.ico";
        source.Branding.PrimaryColor = "#123456";

        var copy = new RealmOptions(source);

        source.Authentication.CookieName = ".mutated";
        source.Csp.Level = CspLevel.Two;
        source.Cors.AllowedOrigins.Add("https://mutated.example");
        source.Logging.SensitiveValuesFilter.Add("mutated-secret");
        source.InputLengthRestrictions.ClientId = 77;
        source.Discovery.CustomEntries["server_copy"] = "mutated";
        source.Discovery.SupportedPromptModes.Add("mutated-prompt");
        source.Endpoints.EnableTokenEndpoint = true;
        source.MutualTls.DomainName = "mutated.example";
        source.Keys.SigningCredentialsAlgorithms.Add(SecurityAlgorithms.EcdsaSha384);
        source.UI.AccessDeniedPath = "/{realm}/account/mutated-denied";
        source.Caching.KeyCacheDuration = TimeSpan.FromMinutes(99);
        source.Account.AllowRememberLogin = true;
        source.Account.RememberMeLoginDuration = TimeSpan.FromDays(1);
        source.Branding.PrimaryColor = "#654321";
        source.AccessTokenJwtType = "mutated+jwt";
        source.EmitScopesAsSpaceDelimitedStringInJwt = false;
        source.DispatchEvents = false;

        Assert.Same(source.ServerOptions, copy.ServerOptions);
        Assert.Equal(".server-copy", copy.Authentication.CookieName);
        Assert.Equal(CspLevel.One, copy.Csp.Level);
        Assert.Contains("https://server-copy.example", copy.Cors.AllowedOrigins);
        Assert.DoesNotContain("https://mutated.example", copy.Cors.AllowedOrigins);
        Assert.Contains("server-copy-secret", copy.Logging.SensitiveValuesFilter);
        Assert.DoesNotContain("mutated-secret", copy.Logging.SensitiveValuesFilter);
        Assert.Equal(31, copy.InputLengthRestrictions.ClientId);
        Assert.Equal("value", copy.Discovery.CustomEntries["server_copy"]);
        Assert.Contains("server-prompt", copy.Discovery.SupportedPromptModes);
        Assert.DoesNotContain("mutated-prompt", copy.Discovery.SupportedPromptModes);
        Assert.False(copy.Endpoints.EnableTokenEndpoint);
        Assert.Equal("mtls.example", copy.MutualTls.DomainName);
        Assert.Contains(SecurityAlgorithms.RsaSha512, copy.Keys.SigningCredentialsAlgorithms);
        Assert.Equal("/{realm}/account/source-denied", copy.UI.AccessDeniedPath);
        Assert.Equal(TimeSpan.FromMinutes(22), copy.Caching.KeyCacheDuration);
        Assert.False(copy.Account.AllowRememberLogin);
        Assert.Equal("source invalid", copy.Account.InvalidCredentialsErrorMessage);
        Assert.Equal(TimeSpan.FromDays(9), copy.Account.RememberMeLoginDuration);
        Assert.Equal("https://cdn.example/logo.svg", copy.Branding.LogoUri);
        Assert.Equal("https://cdn.example/favicon.ico", copy.Branding.FaviconUri);
        Assert.Equal("#123456", copy.Branding.PrimaryColor);
        Assert.Equal("https://issuer.example/source", copy.IssuerUri);
        Assert.False(copy.LowerCaseIssuerUri);
        Assert.False(copy.IncludeRealmPathToIssuerUri);
        Assert.False(copy.StoreAuthorizationParameters);
        Assert.Equal("server-copy+jwt", copy.AccessTokenJwtType);
        Assert.True(copy.EmitScopesAsSpaceDelimitedStringInJwt);
        Assert.True(copy.DispatchEvents);

        Assert.NotSame(source.Authentication, copy.Authentication);
        Assert.NotSame(source.Csp, copy.Csp);
        Assert.NotSame(source.Cors, copy.Cors);
        Assert.NotSame(source.Cors.AllowedOrigins, copy.Cors.AllowedOrigins);
        Assert.NotSame(source.Logging, copy.Logging);
        Assert.NotSame(source.Logging.SensitiveValuesFilter, copy.Logging.SensitiveValuesFilter);
        Assert.NotSame(source.InputLengthRestrictions, copy.InputLengthRestrictions);
        Assert.NotSame(source.Discovery, copy.Discovery);
        Assert.NotSame(source.Discovery.CustomEntries, copy.Discovery.CustomEntries);
        Assert.NotSame(source.Endpoints, copy.Endpoints);
        Assert.NotSame(source.MutualTls, copy.MutualTls);
        Assert.NotSame(source.Keys, copy.Keys);
        Assert.NotSame(source.Keys.SigningCredentialsAlgorithms, copy.Keys.SigningCredentialsAlgorithms);
        Assert.NotSame(source.UI, copy.UI);
        Assert.NotSame(source.Caching, copy.Caching);
        Assert.NotSame(source.Account, copy.Account);
        Assert.NotSame(source.Branding, copy.Branding);
    }

    private async Task<RoyalIdentity.Models.Realm> CreateRealmAsync(string prefix)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var path = $"{prefix}-{suffix}";

        return await manager.CreateAsync(path, $"{path}.test", $"Test Realm {suffix}");
    }

    private static ServerOptions CreateConfiguredServerOptions()
    {
        var serverOptions = new ServerOptions
        {
            Authentication =
            {
                CookieName = ".server-copy",
                CookieLifetime = TimeSpan.FromMinutes(17),
                CookieSlidingExpiration = false,
                CookieSameSiteMode = SameSiteMode.Strict
            },
            Csp =
            {
                Level = CspLevel.One,
                AddDeprecatedHeader = false
            },
            Cors =
            {
                Enabled = true,
                AllowCredentials = true
            },
            InputLengthRestrictions =
            {
                ClientId = 31
            },
            Discovery =
            {
                ShowEndpoints = false,
                ResponseCacheInterval = 90
            },
            Endpoints =
            {
                EnableTokenEndpoint = false,
                EnableJwtRequestUri = true
            },
            MutualTls =
            {
                Enabled = true,
                DomainName = "mtls.example",
                AlwaysEmitConfirmationClaim = true
            },
            Keys =
            {
                MainSigningCredentialsAlgorithm = SecurityAlgorithms.RsaSha512,
                DefaultSigningCredentialsLifetime = TimeSpan.FromDays(12),
                RsaKeySizeInBytes = 4096
            },
            AccessTokenJwtType = "server-copy+jwt",
            EmitScopesAsSpaceDelimitedStringInJwt = true,
            DispatchEvents = true
        };

        serverOptions.Cors.AllowedOrigins.Add("https://server-copy.example");
        serverOptions.Cors.AllowedHeaders.Add("x-server-copy");
        serverOptions.Cors.AllowedMethods.Add("PATCH");
        serverOptions.Logging.SensitiveValuesFilter.Add("server-copy-secret");
        serverOptions.Discovery.CustomEntries["server_copy"] = "value";
        serverOptions.Discovery.SupportedPromptModes.Add("server-prompt");
        serverOptions.Keys.SigningCredentialsAlgorithms.Clear();
        serverOptions.Keys.SigningCredentialsAlgorithms.Add(SecurityAlgorithms.RsaSha512);

        return serverOptions;
    }
}
