using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// The persisted graph of an <see cref="AuthorizationCode"/>. Client, redirect URI and the timestamps stay in
/// relational columns; the subject principal, the resolved resources and the code's own
/// <see cref="AuthorizationCode.Properties"/> live here. The raw code is never persisted — its digest is the
/// key, and the raw value comes back from the lookup argument (plan DF38).
/// </summary>
public sealed class AuthorizationCodePayload
{
	public required string ClientId { get; set; }

	public required string RedirectUri { get; set; }

	public required string SessionState { get; set; }

	public DateTime CreationTime { get; set; }

	public int Lifetime { get; set; }

	public string? RealmId { get; set; }

	public string? Nonce { get; set; }

	public string? StateHash { get; set; }

	public string? SessionId { get; set; }

	public string? CodeChallenge { get; set; }

	public string? CodeChallengeMethod { get; set; }

	/// <summary>
	/// The code's own properties. Unlike the deliberately dropped claim metadata of
	/// <see cref="ClaimPayload"/>, these are part of the operational contract and survive the round-trip.
	/// </summary>
	public Dictionary<string, string>? Properties { get; set; }

	public required ClaimsPrincipalPayload Subject { get; set; }

	public required RequestedResourcesPayload Scopes { get; set; }
}
