using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// JSON codec shared by the operational payload serializers (plan DF9): one version per payload type, and
/// every failure raises <see cref="OperationalPayloadException"/> instead of producing a partially
/// materialized payload.
/// <para>
/// Two options carry that guarantee for the whole family, so no serializer has to re-check member by member:
/// <c>required</c> members make an omitted contract member a failure rather than a silently empty collection,
/// and <see cref="JsonSerializerOptions.RespectNullableAnnotations"/> makes an explicit <c>null</c> on a
/// non-nullable member a failure too. A member declared nullable — an authorization code's
/// <c>Properties</c>, a consent's <c>Scopes</c> — keeps meaning "absent", which is distinct from empty.
/// </para>
/// </summary>
/// <typeparam name="TPayload">The payload DTO.</typeparam>
/// <param name="payloadName">The name used in diagnostics; never the data itself.</param>
/// <param name="currentVersion">The version this build writes and reads.</param>
internal sealed class OperationalPayloadCodec<TPayload>(string payloadName, int currentVersion)
{
	private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.General)
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		RespectNullableAnnotations = true,
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
