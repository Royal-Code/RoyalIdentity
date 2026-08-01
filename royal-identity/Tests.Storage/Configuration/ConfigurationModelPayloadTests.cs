using RoyalIdentity.Options;
using RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

namespace Tests.Storage.Configuration;

/// <summary>
/// Versioned JSON payload serialization for <see cref="ServerOptions"/>/<see cref="RealmOptions"/>
/// (plan Fase 2, DF4/DF5): round-trips are stable and faithful, the circular <c>ServerOptions</c> reference is
/// never serialized (it is re-bound from the authoritative graph on load), and an unknown version or malformed
/// or structurally invalid JSON fails closed instead of returning a partial object.
/// </summary>
public class ConfigurationModelPayloadTests
{
    private readonly ServerOptionsPayloadSerializer serverSerializer = new();
    private readonly RealmOptionsPayloadSerializer realmSerializer = new();

    [Fact]
    public void ServerOptions_RoundTrip_IsStableAndFaithful()
    {
        var options = new ServerOptions
        {
            IssuerUri = "https://issuer.example",
            DispatchEvents = true,
            AccessTokenJwtType = "custom+jwt",
            EmitScopesAsSpaceDelimitedStringInJwt = true,
        };
        options.Keys.MainSigningCredentialsAlgorithm = "RS512";
        options.Cors.AllowedOrigins.Add("https://a.example");

        var (version, json) = serverSerializer.Serialize(options);
        var restored = serverSerializer.Deserialize(version, json);
        var (_, reserialized) = serverSerializer.Serialize(restored);

        Assert.Equal(ServerOptionsPayloadSerializer.CurrentVersion, version);
        Assert.Equal(json, reserialized);
        Assert.Equal("https://issuer.example", restored.IssuerUri);
        Assert.True(restored.DispatchEvents);
        Assert.Equal("custom+jwt", restored.AccessTokenJwtType);
        Assert.Equal("RS512", restored.Keys.MainSigningCredentialsAlgorithm);
        Assert.Contains("https://a.example", restored.Cors.AllowedOrigins);
        // The case-insensitive CORS comparer survives because the get-only collection is repopulated in place.
        Assert.Contains("HTTPS://A.EXAMPLE", restored.Cors.AllowedOrigins);
    }

    [Fact]
    public void ServerOptions_CustomEntries_RoundTrip_PreservesJsonValueSemantics()
    {
        var options = new ServerOptions();
        options.Discovery.CustomEntries["relative"] = "~/metadata";
        options.Discovery.CustomEntries["enabled"] = true;
        options.Discovery.CustomEntries["count"] = 42;
        options.Discovery.CustomEntries["nested"] = new Dictionary<string, object>
        {
            ["text"] = "value",
            ["items"] = new object[] { "one", 2, false },
        };

        var (version, json) = serverSerializer.Serialize(options);
        var restored = serverSerializer.Deserialize(version, json);
        var (_, reserialized) = serverSerializer.Serialize(restored);

        Assert.Equal(json, reserialized);
        Assert.Equal("~/metadata", Assert.IsType<string>(restored.Discovery.CustomEntries["relative"]));
        Assert.True(Assert.IsType<bool>(restored.Discovery.CustomEntries["enabled"]));
        Assert.Equal(42, Assert.IsType<int>(restored.Discovery.CustomEntries["count"]));

        var nested = Assert.IsType<Dictionary<string, object?>>(restored.Discovery.CustomEntries["nested"]);
        Assert.Equal("value", Assert.IsType<string>(nested["text"]));
        var items = Assert.IsType<List<object?>>(nested["items"]);
        Assert.Equal("one", Assert.IsType<string>(items[0]));
        Assert.Equal(2, Assert.IsType<int>(items[1]));
        Assert.False(Assert.IsType<bool>(items[2]));
    }

    [Fact]
    public void ServerOptions_UnknownVersion_FailsClosed()
    {
        var (_, json) = serverSerializer.Serialize(new ServerOptions());

        Assert.Throws<ConfigurationPayloadException>(
            () => serverSerializer.Deserialize(ServerOptionsPayloadSerializer.CurrentVersion + 1, json));
    }

    [Fact]
    public void ServerOptions_MalformedJson_FailsClosed()
    {
        Assert.Throws<ConfigurationPayloadException>(
            () => serverSerializer.Deserialize(ServerOptionsPayloadSerializer.CurrentVersion, "{ not json"));
    }

    [Fact]
    public void ServerOptions_NullGetOnlyCollection_FailsClosed()
    {
        const string json = """{"Keys":{"SigningCredentialsAlgorithms":null}}""";

        Assert.Throws<ConfigurationPayloadException>(
            () => serverSerializer.Deserialize(ServerOptionsPayloadSerializer.CurrentVersion, json));
    }

    [Fact]
    public void RealmOptions_Payload_DoesNotSerializeServerOptions()
    {
        var serverOptions = new ServerOptions { IssuerUri = "https://server.example" };
        var realmOptions = new RealmOptions(serverOptions) { IssuerUri = "https://realm.example" };

        var (_, json) = realmSerializer.Serialize(realmOptions);

        Assert.DoesNotContain("ServerOptions", json);
        Assert.DoesNotContain("https://server.example", json);
        Assert.Contains("https://realm.example", json);
    }

    [Fact]
    public void RealmOptions_RoundTrip_RebindsAuthoritativeServerOptionsAndIsStable()
    {
        var originalServer = new ServerOptions { IssuerUri = "https://server-original.example" };
        var realmOptions = new RealmOptions(originalServer)
        {
            IssuerUri = "https://realm.example",
            StoreAuthorizationParameters = false,
            IncludeRealmPathToIssuerUri = false,
        };

        var (version, json) = realmSerializer.Serialize(realmOptions);

        // Load against a different authoritative server graph; the reference is re-bound, not read from JSON.
        var authoritativeServer = new ServerOptions { IssuerUri = "https://server-authoritative.example" };
        var restored = realmSerializer.Deserialize(version, json, authoritativeServer);
        var (_, reserialized) = realmSerializer.Serialize(restored);

        Assert.Equal(RealmOptionsPayloadSerializer.CurrentVersion, version);
        Assert.Equal(json, reserialized);
        Assert.Same(authoritativeServer, restored.ServerOptions);
        Assert.Equal("https://realm.example", restored.IssuerUri);
        Assert.False(restored.StoreAuthorizationParameters);
        Assert.False(restored.IncludeRealmPathToIssuerUri);
    }

    // plan-data-operational-storage DF29/DF40: the operational options are an additive change to the
    // Configuration family. A payload written before they existed must materialize the closed defaults —
    // without a relational migration and without bumping the payload version.
    [Fact]
    public void RealmOptions_PayloadWithoutTheOperationalOptions_MaterializesTheClosedDefaults()
    {
        var serverOptions = new ServerOptions();
        const string legacyPayload = """{"IssuerUri":"https://realm.example","StoreAuthorizationParameters":true}""";

        var restored = realmSerializer.Deserialize(
            RealmOptionsPayloadSerializer.CurrentVersion, legacyPayload, serverOptions);

        Assert.Equal(1, RealmOptionsPayloadSerializer.CurrentVersion);
        Assert.Equal(600, restored.Authentication.AuthorizationInteractionLifetime);
        Assert.Equal(
            OperationalStorageOptions.DefaultPayloadProtectionProfile,
            restored.OperationalStorage.PayloadProtectionProfile);
        Assert.Equal(JwtAccessTokenPersistenceMode.None, restored.OperationalStorage.JwtAccessTokenPersistence);
        Assert.Equal(RefreshTokenClaimsMode.Current, restored.RefreshTokens.ClaimsMode);
        Assert.Empty(restored.Authentication.Validate());
    }

    [Fact]
    public void RealmOptions_WithTheOperationalOptions_RoundTripsAtTheSameVersion()
    {
        var serverOptions = new ServerOptions();
        var realmOptions = new RealmOptions(serverOptions);
        realmOptions.Authentication.AuthorizationInteractionLifetime = 120;
        realmOptions.OperationalStorage.PayloadProtectionProfile = "vault";
        realmOptions.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Full;
        realmOptions.RefreshTokens.ClaimsMode = RefreshTokenClaimsMode.Snapshot;

        var (version, json) = realmSerializer.Serialize(realmOptions);
        var restored = realmSerializer.Deserialize(version, json, serverOptions);
        var (_, reserialized) = realmSerializer.Serialize(restored);

        Assert.Equal(RealmOptionsPayloadSerializer.CurrentVersion, version);
        Assert.Equal(json, reserialized);
        Assert.Equal(120, restored.Authentication.AuthorizationInteractionLifetime);
        Assert.Equal("vault", restored.OperationalStorage.PayloadProtectionProfile);
        Assert.Equal(JwtAccessTokenPersistenceMode.Full, restored.OperationalStorage.JwtAccessTokenPersistence);
        Assert.Equal(RefreshTokenClaimsMode.Snapshot, restored.RefreshTokens.ClaimsMode);
    }

    // The realm copy constructor is what the snapshot uses; the new options must be copied, not shared.
    [Fact]
    public void RealmOptions_Copy_ClonesTheOperationalOptions()
    {
        var original = new RealmOptions(new ServerOptions());
        original.Authentication.AuthorizationInteractionLifetime = 90;
        original.OperationalStorage.PayloadProtectionProfile = "vault";
        original.OperationalStorage.JwtAccessTokenPersistence = JwtAccessTokenPersistenceMode.Metadata;
        original.RefreshTokens.ClaimsMode = RefreshTokenClaimsMode.Snapshot;

        var copy = new RealmOptions(original);
        copy.OperationalStorage.PayloadProtectionProfile = "other";
        copy.RefreshTokens.ClaimsMode = RefreshTokenClaimsMode.Current;
        copy.Authentication.AuthorizationInteractionLifetime = 30;

        Assert.NotSame(original.OperationalStorage, copy.OperationalStorage);
        Assert.NotSame(original.RefreshTokens, copy.RefreshTokens);
        Assert.Equal("vault", original.OperationalStorage.PayloadProtectionProfile);
        Assert.Equal(JwtAccessTokenPersistenceMode.Metadata, copy.OperationalStorage.JwtAccessTokenPersistence);
        Assert.Equal(RefreshTokenClaimsMode.Snapshot, original.RefreshTokens.ClaimsMode);
        Assert.Equal(90, original.Authentication.AuthorizationInteractionLifetime);
    }

    // DF40: the lifetime is expressed in seconds and must be positive.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RealmOptions_NonPositiveInteractionLifetime_IsAConfigurationError(int lifetime)
    {
        var options = new RealmOptions(new ServerOptions());
        options.Authentication.AuthorizationInteractionLifetime = lifetime;

        Assert.Contains(
            options.Authentication.Validate(),
            error => error.Contains("AuthorizationInteractionLifetime", StringComparison.Ordinal));
    }

    // DF30: an empty profile id is a configuration error — it must never mean "no protection".
    [Fact]
    public void RealmOptions_EmptyProtectionProfile_IsAConfigurationError()
    {
        var options = new RealmOptions(new ServerOptions());
        options.OperationalStorage.PayloadProtectionProfile = "  ";

        Assert.Contains(
            options.OperationalStorage.Validate(),
            error => error.Contains("PayloadProtectionProfile", StringComparison.Ordinal));
    }

    [Fact]
    public void RealmOptions_UnknownVersion_FailsClosed()
    {
        var serverOptions = new ServerOptions();
        var (_, json) = realmSerializer.Serialize(new RealmOptions(serverOptions));

        Assert.Throws<ConfigurationPayloadException>(
            () => realmSerializer.Deserialize(RealmOptionsPayloadSerializer.CurrentVersion + 1, json, serverOptions));
    }

    [Fact]
    public void RealmOptions_MalformedJson_FailsClosed()
    {
        Assert.Throws<ConfigurationPayloadException>(
            () => realmSerializer.Deserialize(RealmOptionsPayloadSerializer.CurrentVersion, "{ not json", new ServerOptions()));
    }

    [Fact]
    public void RealmOptions_NullGetOnlyCollection_FailsClosed()
    {
        const string json = """{"Keys":{"SigningCredentialsAlgorithms":null}}""";

        Assert.Throws<ConfigurationPayloadException>(
            () => realmSerializer.Deserialize(RealmOptionsPayloadSerializer.CurrentVersion, json, new ServerOptions()));
    }

    /// <summary>
    /// A realm persisted before a name joined the redaction set must not keep logging it.
    /// </summary>
    /// <remarks>
    /// The payload carries the whole <c>RealmOptions</c> graph, so it carries the logging configuration too:
    /// raising the default of a configurable list reaches new realms only, and every realm already stored keeps
    /// the older, weaker value. That is why the credentials that must never be logged live in a mandatory floor
    /// instead of in <c>SensitiveValuesFilter</c> — the floor is code, not data, so it applies the moment the
    /// server starts, to every realm, without a payload version bump the version chain has no room for.
    /// </remarks>
    [Fact]
    public void RealmOptions_PersistedWithAnOlderRedactionList_StillRedactsTheMandatoryNames()
    {
        // A payload as it would have been written before code/code_verifier were protected at all.
        const string json = """
            {"Logging":{"SensitiveValuesFilter":["client_secret","password","refresh_token"]}}
            """;

        var restored = realmSerializer.Deserialize(
            RealmOptionsPayloadSerializer.CurrentVersion, json, new ServerOptions());

        Assert.Equal(
            ["client_secret", "password", "refresh_token"],
            restored.Logging.SensitiveValuesFilter);

        Assert.Contains(Constants.Oidc.Token.Request.Code, restored.Logging.RedactedParameterNames);
        Assert.Contains(Constants.Oidc.Token.Request.CodeVerifier, restored.Logging.RedactedParameterNames);
        Assert.Contains(Constants.Oidc.Token.Request.ClientAssertion, restored.Logging.RedactedParameterNames);
    }

    [Fact]
    public void RealmOptions_WithAnEmptiedRedactionList_StillRedactsTheMandatoryNames()
    {
        // The stronger case: configuration actively removing everything cannot switch the protection off.
        const string json = """{"Logging":{"SensitiveValuesFilter":[]}}""";

        var restored = realmSerializer.Deserialize(
            RealmOptionsPayloadSerializer.CurrentVersion, json, new ServerOptions());

        Assert.Empty(restored.Logging.SensitiveValuesFilter);

        foreach (var mandatory in LoggingOptions.AlwaysRedacted)
            Assert.Contains(mandatory, restored.Logging.RedactedParameterNames);
    }
}
