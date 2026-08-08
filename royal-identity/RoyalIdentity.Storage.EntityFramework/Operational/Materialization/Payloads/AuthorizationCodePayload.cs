using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>
/// The persisted graph of an <see cref="AuthorizationCode"/> that has no queryable column of its own: the
/// subject principal, the resolved resources and the code's own <see cref="AuthorizationCode.Properties"/>.
/// <para>
/// Realm, client, redirect URI, session and the timestamps are NOT here — they arrive through
/// <see cref="AuthorizationCodeIdentity"/>. That matters most for this type: the conditional consumption of
/// DF11 matches the client and the redirect URI in the database, so the materialized object must carry exactly
/// the values that condition evaluated, never a second copy that could disagree with it.
/// </para>
/// </summary>
public sealed class AuthorizationCodePayload
{
    public string? Nonce { get; set; }

    public string? CodeChallenge { get; set; }

    public string? CodeChallengeMethod { get; set; }

    /// <summary>
    /// The code's own properties. Unlike the deliberately dropped claim metadata of
    /// <see cref="ClaimPayload"/>, these are part of the operational contract and survive the round-trip.
    /// <c>null</c> is distinct from an empty dictionary, so the round-trip reproduces the model exactly.
    /// </summary>
    public Dictionary<string, string>? Properties { get; set; }

    public required ClaimsPrincipalPayload Subject { get; set; }

    public required RequestedResourcesPayload Scopes { get; set; }
}
