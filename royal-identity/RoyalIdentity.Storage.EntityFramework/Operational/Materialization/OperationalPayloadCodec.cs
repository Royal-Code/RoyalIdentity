using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// JSON codec shared by the operational payload serializers (plan DF9): one version per payload type, and
/// every failure — unknown version, malformed JSON, a missing required member — raises
/// <see cref="OperationalPayloadException"/> instead of producing a partially materialized payload.
/// </summary>
/// <typeparam name="TPayload">The payload DTO.</typeparam>
/// <param name="payloadName">The name used in diagnostics; never the data itself.</param>
/// <param name="currentVersion">The version this build writes and reads.</param>
internal sealed class OperationalPayloadCodec<TPayload>(string payloadName, int currentVersion)
{
	private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.General)
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	public string Serialize(TPayload payload) => JsonSerializer.Serialize(payload, options);

	public TPayload Deserialize(int version, string json)
	{
		if (version != currentVersion)
			throw OperationalPayloadException.UnsupportedVersion(payloadName, version, currentVersion);

		try
		{
			return JsonSerializer.Deserialize<TPayload>(json, options)
				?? throw OperationalPayloadException.EmptyPayload(payloadName);
		}
		catch (JsonException exception)
		{
			throw OperationalPayloadException.InvalidJson(payloadName, exception);
		}
	}
}
