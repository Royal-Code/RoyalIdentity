using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

/// <summary>
/// Serializes the authoritative <see cref="ServerOptions"/> graph to a versioned JSON payload and back
/// (plan DF4/DF5). Serialization/materialization of core option types belongs to the adapter, never to the
/// pure data project. Deserialization is fail-closed: an unknown version or malformed JSON raises
/// <see cref="ConfigurationPayloadException"/> instead of returning a partial object.
/// </summary>
public sealed class ServerOptionsPayloadSerializer
{
	/// <summary>Current payload schema version written by <see cref="Serialize"/>.</summary>
	public const int CurrentVersion = 1;

	private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.General)
	{
		TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { GetOnlyCollectionModifier.Apply } },
		Converters = { new ConfigurationObjectJsonConverter() },
	};

	public (int Version, string Json) Serialize(ServerOptions serverOptions)
	{
		ArgumentNullException.ThrowIfNull(serverOptions);
		return (CurrentVersion, JsonSerializer.Serialize(serverOptions, options));
	}

	public ServerOptions Deserialize(int version, string json)
	{
		if (version != CurrentVersion)
			throw ConfigurationPayloadException.UnsupportedVersion(nameof(ServerOptions), version, CurrentVersion);

		try
		{
			return JsonSerializer.Deserialize<ServerOptions>(json, options)
				?? throw ConfigurationPayloadException.EmptyPayload(nameof(ServerOptions));
		}
		catch (JsonException ex)
		{
			throw ConfigurationPayloadException.InvalidJson(nameof(ServerOptions), ex);
		}
	}
}
