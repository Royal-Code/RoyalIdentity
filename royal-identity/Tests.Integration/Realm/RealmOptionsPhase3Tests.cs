using System.Collections.Specialized;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Endpoints;
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
        realmA.Options.InputLengthRestrictions.ClientId = 12;
        realmA.Options.DispatchEvents = true;
        await storage.Realms.SaveAsync(realmA);

        Assert.True(realmB.Options.Csp.AddDeprecatedHeader);
        Assert.DoesNotContain("realm-a-secret", realmB.Options.Logging.SensitiveValuesFilter);
        Assert.Equal(100, realmB.Options.InputLengthRestrictions.ClientId);
        Assert.False(realmB.Options.DispatchEvents);
        Assert.NotSame(realmA.Options.Csp, realmB.Options.Csp);
        Assert.NotSame(realmA.Options.Logging, realmB.Options.Logging);
        Assert.NotSame(realmA.Options.Logging.SensitiveValuesFilter, realmB.Options.Logging.SensitiveValuesFilter);
        Assert.NotSame(realmA.Options.InputLengthRestrictions, realmB.Options.InputLengthRestrictions);
        Assert.NotSame(storage.ServerOptions.Csp, realmA.Options.Csp);
        Assert.NotSame(storage.ServerOptions.Logging, realmA.Options.Logging);
        Assert.NotSame(storage.ServerOptions.InputLengthRestrictions, realmA.Options.InputLengthRestrictions);
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
        serverOptions.InputLengthRestrictions.GrantType = 33;

        var realmOptions = new RealmOptions(serverOptions);

        Assert.Equal(CspLevel.One, realmOptions.Csp.Level);
        Assert.False(realmOptions.Csp.AddDeprecatedHeader);
        Assert.True(realmOptions.DispatchEvents);
        Assert.Equal(33, realmOptions.InputLengthRestrictions.GrantType);
        Assert.Contains("server-secret", realmOptions.Logging.SensitiveValuesFilter);
        Assert.NotSame(serverOptions.Csp, realmOptions.Csp);
        Assert.NotSame(serverOptions.Logging, realmOptions.Logging);
        Assert.NotSame(serverOptions.Logging.SensitiveValuesFilter, realmOptions.Logging.SensitiveValuesFilter);
        Assert.NotSame(serverOptions.InputLengthRestrictions, realmOptions.InputLengthRestrictions);
    }

    [Fact]
    public async Task InputLengthRestrictions_UsesRealmSpecificGrantTypeLimit()
    {
        const string grantType = "client_credentials";
        var restrictiveRealm = await CreateRealmAsync("limit-low");
        var permissiveRealm = await CreateRealmAsync("limit-ok");
        var storage = factory.Services.GetRequiredService<IStorage>();

        restrictiveRealm.Options.InputLengthRestrictions.GrantType = grantType.Length - 1;
        permissiveRealm.Options.InputLengthRestrictions.GrantType = grantType.Length;
        await storage.Realms.SaveAsync(restrictiveRealm);
        await storage.Realms.SaveAsync(permissiveRealm);

        var endpoint = new TokenEndpoint(
            new EmptyExtensionsGrantsProvider(),
            NullLogger<TokenEndpoint>.Instance);

        var restrictiveResult = await endpoint.TryCreateContextAsync(CreateTokenHttpContext(restrictiveRealm, grantType));
        var permissiveResult = await endpoint.TryCreateContextAsync(CreateTokenHttpContext(permissiveRealm, grantType));

        Assert.False(restrictiveResult.IsValid(out _, out var restrictiveResponse));
        Assert.NotNull(restrictiveResponse);

        Assert.True(permissiveResult.IsValid(out var permissiveContext, out _));
        Assert.IsType<ClientCredentialsContext>(permissiveContext);
    }

    private async Task<RoyalIdentity.Models.Realm> CreateRealmAsync(string prefix)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IRealmManager>();
        var suffix = CryptoRandom.CreateUniqueId(6);
        var path = $"{prefix}-{suffix}";

        return await manager.CreateAsync(path, $"{path}.test", $"Test Realm {suffix}");
    }

    private static DefaultHttpContext CreateTokenHttpContext(RoyalIdentity.Models.Realm realm, string grantType)
    {
        var body = $"{Oidc.Token.Request.GrantType}={Uri.EscapeDataString(grantType)}";
        var bytes = Encoding.UTF8.GetBytes(body);
        var httpContext = new DefaultHttpContext();

        httpContext.Items[Constants.Server.RealmCurrentKey] = realm;
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.ContentLength = bytes.Length;
        httpContext.Request.Body = new MemoryStream(bytes);

        return httpContext;
    }

    private class EmptyExtensionsGrantsProvider : IExtensionsGrantsProvider
    {
        public IReadOnlyList<string> GetAvailableGrantTypes() => [];

        public ValueTask<ITokenEndpointContextBase> CreateContextAsync(string grantType, CancellationToken ct)
            => throw new NotSupportedException();
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
