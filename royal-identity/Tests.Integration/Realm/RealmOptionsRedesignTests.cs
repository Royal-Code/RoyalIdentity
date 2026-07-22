using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Authentication;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Realm;

public class RealmOptionsRedesignTests : IClassFixture<AppFactory>
{
    private readonly AppFactory factory;

    public RealmOptionsRedesignTests(AppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AuthenticationOptions_RealmA_DoesNotAffectRealmB()
    {
        var realmA = await CreateRealmAsync("auth-a");
        var realmB = await CreateRealmAsync("auth-b");
        var storage = factory.Services.GetRequiredService<IStorage>();

        realmA.Options.Authentication.CookieName = ".realm-a";
        realmA.Options.Authentication.CookieLifetime = TimeSpan.FromMinutes(7);
        realmA.Options.Authentication.CookieSlidingExpiration = false;
        realmA.Options.Authentication.CookieSameSiteMode = SameSiteMode.Strict;
        realmA.Options.UI.AccessDeniedPath = "/{realm}/account/denied-a";

        realmB.Options.Authentication.CookieName = ".realm-b";
        realmB.Options.Authentication.CookieLifetime = TimeSpan.FromMinutes(13);
        realmB.Options.Authentication.CookieSlidingExpiration = true;
        realmB.Options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
        realmB.Options.UI.AccessDeniedPath = "/{realm}/account/denied-b";

        await storage.Realms.SaveAsync(realmA);
        await storage.Realms.SaveAsync(realmB);

        var snapshot = new StubConfigurationSnapshot(storage.ServerOptions, realmA, realmB);
        var configure = new ConfigureRealmCookieAuthenticationOptions(snapshot);
        var optionsA = new CookieAuthenticationOptions();
        var optionsB = new CookieAuthenticationOptions();

        configure.Configure($"{Constants.Server.RealmAuthenticationNamePrefix}{realmA.Path}", optionsA);
        configure.Configure($"{Constants.Server.RealmAuthenticationNamePrefix}{realmB.Path}", optionsB);

        Assert.Equal($".realm-a.{realmA.Path}", optionsA.Cookie.Name);
        Assert.Equal(TimeSpan.FromMinutes(7), optionsA.ExpireTimeSpan);
        Assert.False(optionsA.SlidingExpiration);
        Assert.Equal(SameSiteMode.Strict, optionsA.Cookie.SameSite);
        Assert.Equal($"/{realmA.Path}/account/denied-a", optionsA.AccessDeniedPath.Value);

        Assert.Equal($".realm-b.{realmB.Path}", optionsB.Cookie.Name);
        Assert.Equal(TimeSpan.FromMinutes(13), optionsB.ExpireTimeSpan);
        Assert.True(optionsB.SlidingExpiration);
        Assert.Equal(SameSiteMode.Lax, optionsB.Cookie.SameSite);
        Assert.Equal($"/{realmB.Path}/account/denied-b", optionsB.AccessDeniedPath.Value);
    }

    [Fact]
    public async Task RealmOptions_CopyOnCreate_DoesNotShareAuthenticationOptions()
    {
        var realmA = await CreateRealmAsync("copy-a");
        var realmB = await CreateRealmAsync("copy-b");
        var storage = factory.Services.GetRequiredService<IStorage>();

        realmA.Options.Authentication.CookieName = ".changed";
        await storage.Realms.SaveAsync(realmA);

        Assert.Equal(Constants.Server.DefaultCookieName, realmB.Options.Authentication.CookieName);
        Assert.NotSame(realmA.Options.Authentication, realmB.Options.Authentication);
        Assert.NotSame(storage.ServerOptions.Authentication, realmA.Options.Authentication);
        Assert.NotSame(storage.ServerOptions.Authentication, realmB.Options.Authentication);
    }

    private async Task<RoyalIdentity.Models.Realm> CreateRealmAsync(string prefix)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var path = $"{prefix}-{suffix}";

        return await manager.CreateAsync(path, $"{path}.test", $"Test Realm {suffix}");
    }
}
