using System.IdentityModel.Tokens.Jwt;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Serializes the non-queryable graph of a <see cref="RefreshToken"/> to a versioned payload and back
/// (plan DF9). Realm, client and the timestamps come back from <see cref="RefreshTokenIdentity"/>, and
/// consumption state, state version and claims mode are columns too, so nothing here can disagree with what a
/// conditional transition evaluated. The raw handle is the lookup argument and is never persisted (plan DF38).
/// </summary>
public sealed class RefreshTokenPayloadSerializer
{
	/// <summary>Current payload schema version.</summary>
	public const int CurrentVersion = 1;

	private readonly OperationalPayloadCodec<RefreshTokenPayload> codec =
		new(nameof(RefreshToken), CurrentVersion);

	public (int Version, string Json) Serialize(RefreshToken token)
	{
		ArgumentNullException.ThrowIfNull(token);

		var payload = new RefreshTokenPayload
		{
			Issuer = token.Issuer,
			Confirmation = token.Confirmation,
			RequestedScopes = [.. token.RequestedScopes],
			ResourceUris = [.. token.ResourceUris],
			Audiences = [.. token.Audiences],
			AllowedSigningAlgorithms = [.. token.AllowedSigningAlgorithms],
			Claims = [.. token.Claims.Select(ClaimPayload.From)],
		};

		return (CurrentVersion, codec.Serialize(payload));
	}

	/// <param name="version">The persisted payload version.</param>
	/// <param name="json">The persisted payload.</param>
	/// <param name="identity">The authoritative relational identity of the row.</param>
	public RefreshToken Deserialize(int version, string json, RefreshTokenIdentity identity)
	{
		ArgumentNullException.ThrowIfNull(identity);

		var payload = codec.Deserialize(version, json);

		var token = new RefreshToken(
			ClaimValue(payload.Claims, JwtRegisteredClaimNames.Sub),
			ClaimValue(payload.Claims, JwtRegisteredClaimNames.Sid),
			ClaimValue(payload.Claims, JwtRegisteredClaimNames.Jti),
			[.. payload.RequestedScopes],
			identity.ClientId,
			payload.Issuer,
			identity.CreatedAtUtc,
			identity.Lifetime,
			identity.Handle)
		{
			RealmId = identity.RealmId,
			Confirmation = payload.Confirmation,
			Audiences = [.. payload.Audiences],
			AllowedSigningAlgorithms = [.. payload.AllowedSigningAlgorithms],
		};

		// The constructor seeds sub/sid/jti; the persisted claim set is the authority, so it replaces them
		// wholesale and the round-trip cannot gain or lose a claim.
		token.Claims.Clear();
		foreach (var claim in payload.Claims)
			token.Claims.Add(claim.ToClaim());

		foreach (var uri in payload.ResourceUris)
			token.ResourceUris.Add(uri);

		return token;
	}

	private static string ClaimValue(List<ClaimPayload> claims, string type)
		=> claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))?.Value
			?? string.Empty;
}
