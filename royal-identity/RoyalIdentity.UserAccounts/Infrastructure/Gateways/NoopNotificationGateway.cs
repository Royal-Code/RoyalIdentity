namespace RoyalIdentity.UserAccounts.Infrastructure.Gateways;

/// <summary>
/// Default <see cref="INotificationGateway"/> that delivers nothing. It is the safe out-of-the-box behavior for the
/// pure module (a host wires a real transport): in particular it never logs the raw token. Registered with
/// <c>TryAdd</c> so a host implementation overrides it.
/// </summary>
public sealed class NoopNotificationGateway : INotificationGateway
{
	/// <inheritdoc />
	public Task SendPasswordRecoveryAsync(PasswordRecoveryNotification notification, CancellationToken ct = default)
		=> Task.CompletedTask;

	/// <inheritdoc />
	public Task SendEmailVerificationAsync(EmailVerificationNotification notification, CancellationToken ct = default)
		=> Task.CompletedTask;

	/// <inheritdoc />
	public Task SendPhoneVerificationAsync(PhoneVerificationNotification notification, CancellationToken ct = default)
		=> Task.CompletedTask;
}
