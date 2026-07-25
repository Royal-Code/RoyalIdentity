using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization;

/// <summary>
/// Serializes the non-queryable graph of an <see cref="AccessToken"/> to a versioned payload and back
/// (plan DF9). Materialization is independent: it always produces a new graph, so mutating what a store
/// returned never reaches the database without an explicit operation.
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
			ClientId = token.ClientId,
			Issuer = token.Issuer,
			TokenType = token.TokenType,
			AccessTokenType = token.AccessTokenType,
			CreationTime = token.CreationTime,
			Lifetime = token.Lifetime,
			RealmId = token.RealmId,
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
	/// <param name="jti">
	/// The lookup argument. It rematerializes <see cref="AccessToken.Id"/> — and, for a reference token, also
	/// <see cref="TokenBase.Token"/> — so the raw value needs no column of its own (plan DF13).
	/// </param>
	public AccessToken Deserialize(int version, string json, string jti)
	{
		ArgumentException.ThrowIfNullOrEmpty(jti);

		var payload = codec.Deserialize(version, json);

		var token = new AccessToken(
			payload.ClientId,
			payload.Issuer,
			payload.AccessTokenType,
			payload.CreationTime,
			payload.Lifetime,
			jti,
			payload.TokenType)
		{
			RealmId = payload.RealmId,
			Confirmation = payload.Confirmation,
			Audiences = [.. Required(payload.Audiences, nameof(payload.Audiences))],
			AllowedSigningAlgorithms =
				[.. Required(payload.AllowedSigningAlgorithms, nameof(payload.AllowedSigningAlgorithms))],
		};

		if (payload.Token is not null)
			token.Token = payload.Token;

		foreach (var uri in Required(payload.ResourceUris, nameof(payload.ResourceUris)))
			token.ResourceUris.Add(uri);

		foreach (var claim in Required(payload.Claims, nameof(payload.Claims)))
			token.Claims.Add(claim.ToClaim());

		return token;
	}

	private static List<T> Required<T>(List<T>? values, string name)
		=> values ?? throw OperationalPayloadException.IncompletePayload(nameof(AccessToken), $"'{name}' is null");
}
