using RoyalIdentity.UserAccounts.Infrastructure.Gateways;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Result of a phone verification request. The public HTTP/UI response remains generic, while a non-null
/// <see cref="Notification"/> gives the trusted edge a delivery payload after the command unit of work completes.
/// </summary>
/// <param name="Notification">The delivery payload, or <c>null</c> when no eligible phone/token was produced.</param>
public sealed record PhoneVerificationRequestResult(PhoneVerificationNotification? Notification)
{
	/// <summary>
	/// Gets the anti-enumeration success result that carries no delivery payload.
	/// </summary>
	public static PhoneVerificationRequestResult NoDelivery { get; } = new((PhoneVerificationNotification?)null);

	/// <summary>
	/// Creates a result carrying a delivery payload.
	/// </summary>
	/// <param name="notification">The phone verification notification to deliver.</param>
	/// <returns>A result with the delivery payload.</returns>
	public static PhoneVerificationRequestResult Deliver(PhoneVerificationNotification notification)
		=> new(notification);
}
