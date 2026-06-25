namespace RoyalIdentity.UserAccounts.Infrastructure.Gateways;

/// <summary>
/// The delivery payload for a password recovery message (ADR-017 §2.4). Carries the <em>raw</em> recovery token,
/// which appears here exactly once on its way to the user and must never be persisted, logged, audited or placed in
/// an event. The host-provided gateway is responsible for rendering and sending the message (email/SMS/etc.).
/// </summary>
/// <param name="RealmId">The owning realm.</param>
/// <param name="SubjectId">The recipient account subject identifier.</param>
/// <param name="DisplayName">The recipient display name.</param>
/// <param name="Address">The destination address the token was issued for (the primary email).</param>
/// <param name="Token">The raw recovery token (single-use, transported once).</param>
/// <param name="ExpiresAt">When the token expires.</param>
public sealed record PasswordRecoveryNotification(
	string RealmId,
	string SubjectId,
	string DisplayName,
	string Address,
	string Token,
	DateTimeOffset ExpiresAt);
