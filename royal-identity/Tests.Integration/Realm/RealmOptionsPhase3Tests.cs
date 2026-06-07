using System.Collections.Specialized;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Events;
using RoyalIdentity.Options;
using RoyalIdentity.Responses.HttpResults;
using RoyalIdentity.Utils;
using Tests.Integration.Prepare;

namespace Tests.Integration.Realm;

public class RealmOptionsPhase3Tests : IClassFixture<RealmOptionsPhase3Tests.EventObserverAppFactory>
{
    private readonly EventObserverAppFactory factory;

    public RealmOptionsPhase3Tests(EventObserverAppFactory factory)
    {
        this.factory = factory;
        factory.ObservedEvents.Reset();
    }

    [Fact]
    public async Task CspOptions_UsesRealmSpecificPolicy()
    {
        var realm = await CreateRealmAsync("csp");
        var storage = factory.Services.GetRequiredService<IStorage>();

        realm.Options.Csp.Level = CspLevel.One;
        realm.Options.Csp.AddDeprecatedHeader = false;
        await storage.Realms.SaveAsync(realm);

        using var scope = factory.Services.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider
        };
        httpContext.Items[Constants.Server.RealmCurrentKey] = realm;
        httpContext.Response.Body = new MemoryStream();

        var result = new ResponseToFormPostResult(
            "https://client.example/callback",
            new NameValueCollection { [Constants.Oidc.Authorize.Response.Code] = "code" });

        await result.ExecuteAsync(httpContext);

        var csp = httpContext.Response.Headers.ContentSecurityPolicy.ToString();
        Assert.Contains("'unsafe-inline'", csp);
        Assert.False(httpContext.Response.Headers.ContainsKey("X-Content-Security-Policy"));
    }

    [Fact]
    public async Task Events_DispatchEventsFalse_DisablesOnlyThatRealm()
    {
        var disabledRealm = await CreateRealmAsync("evt-off");
        var enabledRealm = await CreateRealmAsync("evt-on");
        var storage = factory.Services.GetRequiredService<IStorage>();

        disabledRealm.Options.DispatchEvents = false;
        enabledRealm.Options.DispatchEvents = true;
        await storage.Realms.SaveAsync(disabledRealm);
        await storage.Realms.SaveAsync(enabledRealm);

        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();

        await dispatcher.DispatchAsync(new TestEvent("disabled"), disabledRealm);
        await dispatcher.DispatchAsync(new TestEvent("enabled"), enabledRealm);

        var evt = Assert.Single(factory.ObservedEvents.Events.OfType<TestEvent>());
        Assert.Equal("enabled", evt.Marker);
        Assert.Equal(enabledRealm.Id, evt.RealmId);
    }

    [Fact]
    public async Task RealmOptions_CopyOnCreate_DoesNotSharePhase3Options()
    {
        var realmA = await CreateRealmAsync("phase3-a");
        var realmB = await CreateRealmAsync("phase3-b");
        var storage = factory.Services.GetRequiredService<IStorage>();

        realmA.Options.Csp.AddDeprecatedHeader = false;
        realmA.Options.Logging.SensitiveValuesFilter.Add("realm-a-secret");
        realmA.Options.DispatchEvents = true;
        await storage.Realms.SaveAsync(realmA);

        Assert.True(realmB.Options.Csp.AddDeprecatedHeader);
        Assert.DoesNotContain("realm-a-secret", realmB.Options.Logging.SensitiveValuesFilter);
        Assert.False(realmB.Options.DispatchEvents);
        Assert.NotSame(realmA.Options.Csp, realmB.Options.Csp);
        Assert.NotSame(realmA.Options.Logging, realmB.Options.Logging);
        Assert.NotSame(realmA.Options.Logging.SensitiveValuesFilter, realmB.Options.Logging.SensitiveValuesFilter);
        Assert.NotSame(storage.ServerOptions.Csp, realmA.Options.Csp);
        Assert.NotSame(storage.ServerOptions.Logging, realmA.Options.Logging);
    }

    [Fact]
    public void RealmOptions_CopyFromServer_PropagatesPhase3Values()
    {
        var serverOptions = new ServerOptions
        {
            Csp =
            {
                Level = CspLevel.One,
                AddDeprecatedHeader = false
            },
            DispatchEvents = true
        };
        serverOptions.Logging.SensitiveValuesFilter.Add("server-secret");

        var realmOptions = new RealmOptions(serverOptions);

        Assert.Equal(CspLevel.One, realmOptions.Csp.Level);
        Assert.False(realmOptions.Csp.AddDeprecatedHeader);
        Assert.True(realmOptions.DispatchEvents);
        Assert.Contains("server-secret", realmOptions.Logging.SensitiveValuesFilter);
        Assert.NotSame(serverOptions.Csp, realmOptions.Csp);
        Assert.NotSame(serverOptions.Logging, realmOptions.Logging);
        Assert.NotSame(serverOptions.Logging.SensitiveValuesFilter, realmOptions.Logging.SensitiveValuesFilter);
    }

    private async Task<RoyalIdentity.Models.Realm> CreateRealmAsync(string prefix)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var path = $"{prefix}-{suffix}";

        return await manager.CreateAsync(path, $"{path}.test", $"Test Realm {suffix}");
    }

    public class EventObserverAppFactory : AppFactory
    {
        public ObservedEvents ObservedEvents { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(ObservedEvents);
                services.AddSingleton<IEventObserver<TestEvent>, TestEventObserver>();
            });
        }
    }

    public class ObservedEvents
    {
        public List<Event> Events { get; } = [];

        public void Reset() => Events.Clear();

        public void Add(Event evt) => Events.Add(evt);
    }

    public class TestEventObserver(ObservedEvents observedEvents) : IEventObserver<TestEvent>
    {
        public Task HandleAsync(TestEvent evt)
        {
            observedEvents.Add(evt);
            return Task.CompletedTask;
        }
    }

    public class TestEvent(string marker) : Event("Test", "Test Event", EventTypes.Information)
    {
        public string Marker { get; } = marker;
    }
}
