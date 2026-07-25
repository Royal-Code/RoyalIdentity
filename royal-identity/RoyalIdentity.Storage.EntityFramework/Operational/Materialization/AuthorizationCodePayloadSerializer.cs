using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Serializes the non-queryable graph of an <see cref="AuthorizationCode"/> to a versioned payload and back
/// (plan DF9): the subject principal, the resolved resources and the code's own properties. Client, redirect
/// URI and timestamps are relational columns, and the raw code is the lookup argument (plan DF38).
/// </summary>
public sealed class AuthorizationCodePayloadSerializer
{
	/// <summary>Current payload schema version.</summary>
	public const int CurrentVersion = 1;

	private readonly OperationalPayloadCodec<AuthorizationCodePayload> codec =
		new(nameof(AuthorizationCode), CurrentVersion);

	public (int Version, string Json) Serialize(AuthorizationCode code)
	{
		ArgumentNullException.ThrowIfNull(code);

		var payload = new AuthorizationCodePayload
		{
			ClientId = code.ClientId,
			RedirectUri = code.RedirectUri,
			SessionState = code.SessionState,
			CreationTime = code.CreationTime,
			Lifetime = code.Lifetime,
			RealmId = code.RealmId,
			Nonce = code.Nonce,
			StateHash = code.StateHash,
			SessionId = code.SessionId,
			CodeChallenge = code.CodeChallenge,
			CodeChallengeMethod = code.CodeChallengeMethod,
			Properties = code.Properties is null ? null : new Dictionary<string, string>(code.Properties),
			Subject = ClaimsPrincipalPayload.From(code.Subject),
			Scopes = RequestedResourcesPayload.From(code.Scopes),
		};

		return (CurrentVersion, codec.Serialize(payload));
	}

	/// <param name="version">The persisted payload version.</param>
	/// <param name="json">The persisted payload.</param>
	/// <param name="code">The lookup argument; it rematerializes <see cref="AuthorizationCode.Code"/>.</param>
	public AuthorizationCode Deserialize(int version, string json, string code)
	{
		ArgumentException.ThrowIfNullOrEmpty(code);

		var payload = codec.Deserialize(version, json);

		return new AuthorizationCode(
			code,
			payload.ClientId,
			payload.Subject.ToClaimsPrincipal(nameof(AuthorizationCode)),
			payload.SessionState,
			payload.CreationTime,
			payload.Lifetime,
			payload.Scopes.ToRequestedResources(nameof(AuthorizationCode)),
			payload.RedirectUri)
		{
			RealmId = payload.RealmId,
			Nonce = payload.Nonce,
			StateHash = payload.StateHash,
			SessionId = payload.SessionId,
			CodeChallenge = payload.CodeChallenge,
			CodeChallengeMethod = payload.CodeChallengeMethod,
			Properties = payload.Properties is null ? null : new Dictionary<string, string>(payload.Properties),
		};
	}
}
