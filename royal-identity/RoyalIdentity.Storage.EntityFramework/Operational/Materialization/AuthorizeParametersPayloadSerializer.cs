using System.Collections.Specialized;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Serializes the authorize parameters of a stored continuation to a versioned payload and back
/// (plan DF9/DF16). Repeated keys and null values survive the round-trip, so the materialized collection
/// behaves exactly like the one that was written.
/// </summary>
public sealed class AuthorizeParametersPayloadSerializer
{
	/// <summary>Current payload schema version.</summary>
	public const int CurrentVersion = 1;

	private const string PayloadName = "AuthorizeParameters";

	private readonly OperationalPayloadCodec<AuthorizeParametersPayload> codec =
		new(PayloadName, CurrentVersion);

	public (int Version, string Json) Serialize(NameValueCollection parameters)
	{
		ArgumentNullException.ThrowIfNull(parameters);

		var payload = new AuthorizeParametersPayload { Parameters = [] };

		for (var index = 0; index < parameters.Count; index++)
		{
			// A key whose only value is null has no value array at all; it is preserved as a single null.
			var values = parameters.GetValues(index);

			payload.Parameters.Add(new AuthorizeParameterPayload
			{
				Name = parameters.GetKey(index),
				Values = values is null ? [null] : [.. values],
			});
		}

		return (CurrentVersion, codec.Serialize(payload));
	}

	public NameValueCollection Deserialize(int version, string json)
	{
		var payload = codec.Deserialize(version, json);

		var parameters = new NameValueCollection();
		foreach (var parameter in payload.Parameters)
		{
			foreach (var value in parameter.Values)
				parameters.Add(parameter.Name, value);
		}

		return parameters;
	}
}
