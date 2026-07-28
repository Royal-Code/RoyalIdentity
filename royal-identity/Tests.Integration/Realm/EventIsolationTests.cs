using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Events;
using RoyalIdentity.Extensions;
using RoyalIdentity.Utils;
using System.Net.Http.Json;
using Tests.Integration.Prepare;

namespace Tests.Integration.Realm;

/// <summary>
/// End-to-end tests for Phase 5 (realm-scoped events).
/// Uses EventCapturingAppFactory to intercept all dispatched events.
/// </summary>
public class EventIsolationTests : IClassFixture<EventCapturingAppFactory>
{
    private readonly EventCapturingAppFactory factory;

    public EventIsolationTests(EventCapturingAppFactory factory)
    {
        this.factory = factory;
        // Tests in this class share the same factory — clear events before each test.
        factory.EventCapture.Reset();
    }

    // ─── §8.7 ClientCredentialsFlow_EventsContainRealmId ──────────────────────

    [Fact]
    public async Task ClientCredentialsFlow_EventsContainRealmId()
    {
        // Arrange: register a CC client in DemoRealm
        var clientId = $"evt-cc-{CryptoRandom.CreateUniqueId(6)}";
        var secret = CryptoRandom.CreateUniqueId();
        await factory.SaveClientAsync(factory.Handles.Demo, clientId, registered =>
        {
            registered.Name = "Event Capture CC Client";
            registered.RequireClientSecret = true;
            registered.AllowedGrantTypes.Add("client_credentials");
            registered.AllowedScopes.Add("api");
            registered.AllowedResourceServers.Add("apiserver");
            registered.AllowedResponseTypes.Add("code");
            registered.Secrets.Add(new RoyalIdentity.Models.ClientSecret(secret.Sha512()));
        });

        var client = factory.CreateClient();
        var url = Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path);

        // Act
        var response = await client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = "api"
        }));
        response.EnsureSuccessStatusCode();

        // Assert: at least one event was captured and all captured events carry the realm ID
        Assert.NotEmpty(factory.EventCapture.Events);
        Assert.All(factory.EventCapture.Events, evt =>
            Assert.Equal(factory.Handles.Demo.Id, evt.RealmId));

        // Specific: the access token event exists and has the correct realm
        var atEvent = factory.EventCapture.Events.OfType<AccessTokenIssuedEvent>().SingleOrDefault();
        Assert.NotNull(atEvent);
        Assert.Equal(factory.Handles.Demo.Id, atEvent.RealmId);
    }

    // ─── §8.7 UnknownRealmEvent_HasNoRealmId ──────────────────────────────────

    [Fact]
    public async Task UnknownRealmEvent_HasNoRealmId()
    {
        // Arrange
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        // Act: request to a realm that doesn't exist triggers RealmNotFoundEvent
        await client.GetAsync(Oidc.Routes.BuildAuthorizeUrl("nonexistent-realm-evt"));

        // Assert: event was dispatched via the non-realm overload → RealmId must be null
        var notFoundEvent = factory.EventCapture.Events.OfType<RealmNotFoundEvent>().SingleOrDefault();
        Assert.NotNull(notFoundEvent);
        Assert.Null(notFoundEvent.RealmId);

        // The event carries the realm path string that failed lookup
        Assert.Equal("nonexistent-realm-evt", notFoundEvent.Realm);
    }
}
