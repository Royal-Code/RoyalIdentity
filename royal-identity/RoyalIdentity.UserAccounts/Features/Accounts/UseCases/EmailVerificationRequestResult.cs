using RoyalIdentity.UserAccounts.Infrastructure.Gateways;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// Result of an email verification request. The public HTTP/UI response remains generic, while a non-null
/// <see cref="Notification"/> gives the trusted edge a delivery payload after the command unit of work completes.
/// </summary>
/// <param name="Notification">The delivery payload, or <c>null</c> when no eligible email/token was produced.</param>
public sealed record EmailVerificationRequestResult(EmailVerificationNotification? Notification)
{
	/// <summary>
	/// Gets the anti-enumeration success result that carries no delivery payload.
	/// </summary>
	public static EmailVerificationRequestResult NoDelivery { get; } = new((EmailVerificationNotification?)null);

	/// <summary>
	/// Creates a result carrying a delivery payload.
	/// </summary>
	/// <param name="notification">The email verification notification to deliver.</param>
	/// <returns>A result with the delivery payload.</returns>
	public static EmailVerificationRequestResult Deliver(EmailVerificationNotification notification)
		=> new(notification);
}
