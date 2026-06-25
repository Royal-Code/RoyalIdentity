namespace RoyalIdentity.UserAccounts.Features.Accounts.Domain;

/// <summary>
/// Why a <see cref="UserAccountActionToken"/> was revoked before being consumed (ADR-017 §2.4). Recorded for
/// audit/diagnostics; the revoked token can never be consumed afterwards.
/// </summary>
public enum ActionTokenRevocationReason
{
	/// <summary>Revoked because a newer token of the same purpose was issued for the account.</summary>
	Superseded
}
