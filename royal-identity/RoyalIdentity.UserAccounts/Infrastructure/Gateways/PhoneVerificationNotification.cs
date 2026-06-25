namespace RoyalIdentity.UserAccounts.Infrastructure.Gateways;

/// <summary>
/// The delivery payload for a phone verification message (ADR-017 §2.8). Carries the <em>raw</em> verification
/// token/code, which appears here once on its way to the user and must never be persisted, logged, audited or
/// placed in an event. The token is bound to <paramref name="Number"/> — the number being verified.
/// </summary>
/// <param name="RealmId">The owning realm.</param>
/// <param name="SubjectId">The recipient account subject identifier.</param>
/// <param name="DisplayName">The recipient display name.</param>
/// <param name="Number">The phone number being verified (the token target).</param>
/// <param name="Token">The raw verification token/code (single-use, transported once).</param>
/// <param name="ExpiresAt">When the token expires.</param>
public sealed record PhoneVerificationNotification(
	string RealmId,
	string SubjectId,
	string DisplayName,
	string Number,
	string Token,
	DateTimeOffset ExpiresAt);
