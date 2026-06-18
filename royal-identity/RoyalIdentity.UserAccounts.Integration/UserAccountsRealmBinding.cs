using RoyalIdentity.UserAccounts.Options;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Realm-bound module inputs exposed to the integration adapter without leaking the rich IdP realm downstream.
/// </summary>
/// <param name="RealmId">The realm identifier used by the UserAccounts module.</param>
/// <param name="Options">The account policies resolved for the realm.</param>
public sealed record UserAccountsRealmBinding(string RealmId, UserAccountsRealmOptions Options);
