using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Serializes the non-queryable graph of an <see cref="AccessToken"/> to a versioned payload and back
/// (plan DF9). Whatever a relational column holds is never written here and always comes back from
/// <see cref="AccessTokenIdentity"/>, so payload and columns cannot disagree. Materialization is independent:
/// it always produces a new graph, so mutating what a store returned never reaches the database without an
/// explicit operation.
/// </summary>
public sealed class AccessTokenPayloadSerializer
{
	/// <summary>Current payload schema version.</summary>
	public const int CurrentVersion = 1;

	private readonly OperationalPayloadCodec<AccessTokenPayload> codec =
		new(nameof(AccessToken), CurrentVersion);

	public (int Version, string Json) Serialize(AccessToken token)
	{
		ArgumentNullException.ThrowIfNull(token);

		var payload = new AccessTokenPayload
		{
			Issuer = token.Issuer,
			TokenType = token.TokenType,
			Confirmation = token.Confirmation,
			// DF13/DF38: the reference bearer coincides with the jti, which is the lookup argument, so it is
			// never copied here. Only a compact JWT — which differs from the jti — is persisted.
			Token = string.Equals(token.Token, token.Id, StringComparison.Ordinal) ? null : token.Token,
			Audiences = [.. token.Audiences],
			AllowedSigningAlgorithms = [.. token.AllowedSigningAlgorithms],
			ResourceUris = [.. token.ResourceUris],
			Claims = [.. token.Claims.Select(ClaimPayload.From)],
		};

		return (CurrentVersion, codec.Serialize(payload));
	}

	/// <param name="version">The persisted payload version.</param>
	/// <param name="json">The persisted payload.</param>
	/// <param name="identity">The authoritative relational identity of the row.</param>
	public AccessToken Deserialize(int version, string json, AccessTokenIdentity identity)
	{
		ArgumentNullException.ThrowIfNull(identity);

		var payload = codec.Deserialize(version, json);

		var token = new AccessToken(
			identity.ClientId,
			payload.Issuer,
			identity.AccessTokenType,
			identity.CreatedAtUtc,
			identity.Lifetime,
			identity.Jti,
			payload.TokenType)
		{
			RealmId = identity.RealmId,
			Confirmation = payload.Confirmation,
			Audiences = [.. payload.Audiences],
			AllowedSigningAlgorithms = [.. payload.AllowedSigningAlgorithms],
		};

		if (payload.Token is not null)
			token.Token = payload.Token;

		foreach (var uri in payload.ResourceUris)
			token.ResourceUris.Add(uri);

		foreach (var claim in payload.Claims)
			token.Claims.Add(claim.ToClaim());

		return token;
	}
}
