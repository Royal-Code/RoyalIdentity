using System.IdentityModel.Tokens.Jwt;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Serializes the non-queryable graph of a <see cref="RefreshToken"/> to a versioned payload and back
/// (plan DF9). Consumption state, state version and claims mode are relational columns, so they are not here;
/// the raw handle is the lookup argument and is never persisted (plan DF38).
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
			ClientId = token.ClientId,
			Issuer = token.Issuer,
			CreationTime = token.CreationTime,
			Lifetime = token.Lifetime,
			RealmId = token.RealmId,
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
	/// <param name="handle">The lookup argument; it rematerializes <see cref="TokenBase.Token"/>.</param>
	public RefreshToken Deserialize(int version, string json, string handle)
	{
		ArgumentException.ThrowIfNullOrEmpty(handle);

		var payload = codec.Deserialize(version, json);
		var claims = Required(payload.Claims, nameof(payload.Claims));

		var token = new RefreshToken(
			ClaimValue(claims, JwtRegisteredClaimNames.Sub),
			ClaimValue(claims, JwtRegisteredClaimNames.Sid),
			ClaimValue(claims, JwtRegisteredClaimNames.Jti),
			[.. Required(payload.RequestedScopes, nameof(payload.RequestedScopes))],
			payload.ClientId,
			payload.Issuer,
			payload.CreationTime,
			payload.Lifetime,
			handle)
		{
			RealmId = payload.RealmId,
			Confirmation = payload.Confirmation,
			Audiences = [.. Required(payload.Audiences, nameof(payload.Audiences))],
			AllowedSigningAlgorithms =
				[.. Required(payload.AllowedSigningAlgorithms, nameof(payload.AllowedSigningAlgorithms))],
		};

		// The constructor seeds sub/sid/jti; the persisted claim set is the authority, so it replaces them
		// wholesale and the round-trip cannot gain or lose a claim.
		token.Claims.Clear();
		foreach (var claim in claims)
			token.Claims.Add(claim.ToClaim());

		foreach (var uri in Required(payload.ResourceUris, nameof(payload.ResourceUris)))
			token.ResourceUris.Add(uri);

		return token;
	}

	private static string ClaimValue(List<ClaimPayload> claims, string type)
		=> claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))?.Value
			?? string.Empty;

	private static List<T> Required<T>(List<T>? values, string name)
		=> values ?? throw OperationalPayloadException.IncompletePayload(nameof(RefreshToken), $"'{name}' is null");
}
