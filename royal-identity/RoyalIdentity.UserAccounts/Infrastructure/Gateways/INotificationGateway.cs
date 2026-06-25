namespace RoyalIdentity.UserAccounts.Infrastructure.Gateways;

/// <summary>
/// Abstract outbound notification gateway for account security flows (architecture.md §6 / ADR-017 §2.13). The pure
/// module defines the seam and ships a no-op default; a host supplies the real transport (email/SMS). Delivery
/// payloads carry the raw token exactly once — implementations must never persist, log or audit it.
/// </summary>
public interface INotificationGateway
{
	/// <summary>
	/// Delivers a password recovery message containing the raw recovery token.
	/// </summary>
	/// <param name="notification">The recovery delivery payload.</param>
	/// <param name="ct">A cancellation token.</param>
	Task SendPasswordRecoveryAsync(PasswordRecoveryNotification notification, CancellationToken ct = default);

	/// <summary>
	/// Delivers an email verification message containing the raw verification token.
	/// </summary>
	/// <param name="notification">The email verification delivery payload.</param>
	/// <param name="ct">A cancellation token.</param>
	Task SendEmailVerificationAsync(EmailVerificationNotification notification, CancellationToken ct = default);

	/// <summary>
	/// Delivers a phone verification message containing the raw verification token/code.
	/// </summary>
	/// <param name="notification">The phone verification delivery payload.</param>
	/// <param name="ct">A cancellation token.</param>
	Task SendPhoneVerificationAsync(PhoneVerificationNotification notification, CancellationToken ct = default);
}
