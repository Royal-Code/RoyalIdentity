namespace RoyalIdentity.UserAccounts.Infrastructure.Gateways;

/// <summary>
/// The delivery payload for an email verification message (ADR-017 §2.8). Carries the <em>raw</em> verification
/// token, which appears here once on its way to the user and must never be persisted, logged, audited or placed in
/// an event. The token is bound to <paramref name="Address"/> — the address being verified.
/// </summary>
/// <param name="RealmId">The owning realm.</param>
/// <param name="SubjectId">The recipient account subject identifier.</param>
/// <param name="DisplayName">The recipient display name.</param>
/// <param name="Address">The email address being verified (the token target).</param>
/// <param name="Token">The raw verification token (single-use, transported once).</param>
/// <param name="ExpiresAt">When the token expires.</param>
public sealed record EmailVerificationNotification(
	string RealmId,
	string SubjectId,
	string DisplayName,
	string Address,
	string Token,
	DateTimeOffset ExpiresAt);
