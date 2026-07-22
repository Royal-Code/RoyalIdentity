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
}
