using RoyalIdentity.Models;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Serializes the consented scopes of a <see cref="Consent"/> to a versioned payload and back (plan DF9/DF14).
/// Realm, subject, client and the timestamps are the relational identity and are supplied by the store.
/// </summary>
public sealed class ConsentPayloadSerializer
{
	/// <summary>Current payload schema version.</summary>
	public const int CurrentVersion = 1;

	private readonly OperationalPayloadCodec<ConsentPayload> codec = new(nameof(Consent), CurrentVersion);

	public (int Version, string Json) Serialize(Consent consent)
	{
		ArgumentNullException.ThrowIfNull(consent);

		var payload = new ConsentPayload
		{
			Scopes = consent.Scopes is null
				? null
				: [.. consent.Scopes.Select(ConsentedScopePayload.From)],
		};

		return (CurrentVersion, codec.Serialize(payload));
	}

	/// <param name="version">The persisted payload version.</param>
	/// <param name="json">The persisted payload.</param>
	/// <param name="realmId">The realm of the relational identity.</param>
	/// <param name="subjectId">The subject of the relational identity.</param>
	/// <param name="clientId">The client of the relational identity.</param>
	/// <param name="creationTime">The persisted creation time.</param>
	/// <param name="expiration">The persisted expiration, or <c>null</c> when the consent does not expire.</param>
	public Consent Deserialize(
		int version,
		string json,
		string realmId,
		string subjectId,
		string clientId,
		DateTime creationTime,
		DateTime? expiration)
	{
		var payload = codec.Deserialize(version, json);

		return new Consent
		{
			RealmId = realmId,
			SubjectId = subjectId,
			ClientId = clientId,
			CreationTime = creationTime,
			Expiration = expiration,
			Scopes = payload.Scopes is null
				? null
				: [.. payload.Scopes.Select(scope => scope.ToConsentedScope())],
		};
	}
}
