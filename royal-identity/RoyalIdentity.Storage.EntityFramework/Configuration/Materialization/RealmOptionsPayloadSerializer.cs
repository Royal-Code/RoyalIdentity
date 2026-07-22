using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using RoyalIdentity.Options;

namespace RoyalIdentity.Storage.EntityFramework.Configuration.Materialization;

/// <summary>
/// Serializes a realm's <see cref="RealmOptions"/> to a versioned JSON payload and back (plan DF4/DF5),
/// deliberately excluding the circular <see cref="RealmOptions.ServerOptions"/> reference: the authoritative
/// <see cref="ServerOptions"/> is supplied by the caller (from the snapshot) at materialization time, so the
/// payload never re-embeds — or drifts from — the server graph. Deserialization is fail-closed via
/// <see cref="ConfigurationPayloadException"/>.
/// </summary>
public sealed class RealmOptionsPayloadSerializer
{
	/// <summary>Current payload schema version written by <see cref="Serialize"/>.</summary>
	public const int CurrentVersion = 1;

	// The ServerOptions navigation is dropped on both read and write, so the payload is identical regardless
	// of which server graph a realm is bound to.
	private static readonly JsonSerializerOptions serializeOptions = new(JsonSerializerDefaults.General)
	{
		TypeInfoResolver = new DefaultJsonTypeInfoResolver
		{
			Modifiers = { DropServerOptionsNavigation, GetOnlyCollectionModifier.Apply }
		},
		Converters = { new ConfigurationObjectJsonConverter() },
	};

	public (int Version, string Json) Serialize(RealmOptions realmOptions)
	{
		ArgumentNullException.ThrowIfNull(realmOptions);
		return (CurrentVersion, JsonSerializer.Serialize(realmOptions, serializeOptions));
	}

	public RealmOptions Deserialize(int version, string json, ServerOptions serverOptions)
	{
		ArgumentNullException.ThrowIfNull(serverOptions);

		if (version != CurrentVersion)
			throw ConfigurationPayloadException.UnsupportedVersion(nameof(RealmOptions), version, CurrentVersion);

		// The RealmOptions constructor requires the authoritative ServerOptions, so object creation closes over
		// the caller-supplied graph; the dropped navigation is never read from the payload.
		var deserializeOptions = new JsonSerializerOptions(JsonSerializerDefaults.General)
		{
			TypeInfoResolver = new DefaultJsonTypeInfoResolver
			{
				Modifiers =
				{
					DropServerOptionsNavigation,
					GetOnlyCollectionModifier.Apply,
					typeInfo =>
					{
						if (typeInfo.Type == typeof(RealmOptions))
							typeInfo.CreateObject = () => new RealmOptions(serverOptions);
					}
				}
			},
			Converters = { new ConfigurationObjectJsonConverter() },
		};

		try
		{
			return JsonSerializer.Deserialize<RealmOptions>(json, deserializeOptions)
				?? throw ConfigurationPayloadException.EmptyPayload(nameof(RealmOptions));
		}
		catch (JsonException ex)
		{
			throw ConfigurationPayloadException.InvalidJson(nameof(RealmOptions), ex);
		}
	}

	private static void DropServerOptionsNavigation(JsonTypeInfo typeInfo)
	{
		if (typeInfo.Type != typeof(RealmOptions))
			return;

		var serverOptions = typeInfo.Properties
			.FirstOrDefault(p => p.Name == nameof(RealmOptions.ServerOptions));

		if (serverOptions is not null)
			typeInfo.Properties.Remove(serverOptions);
	}
}
