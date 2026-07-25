namespace RoyalIdentity.Options;

/// <summary>
/// Where the claims of a token issued through a refresh come from
/// (plan-data-operational-storage DF32). This is a realm-only policy: there is no per-client override, and
/// neither mode ever widens the scopes/resources of the original grant, nor depends on the row of the previous
/// access token.
/// </summary>
public enum RefreshTokenClaimsMode
{
    /// <summary>
    /// The refresh token keeps only the minimal grant (subject, session, client, scopes, resources and the
    /// protocol context). Each renewal revalidates account/session/configuration and re-runs issuance against
    /// the current claims. The default.
    /// </summary>
    Current = 0,

    /// <summary>
    /// The refresh token keeps, in its own payload, everything needed to reproduce the claims it was issued
    /// with. Account, session, client, expiration and consumption are still validated on every renewal.
    /// </summary>
    Snapshot = 1
}
