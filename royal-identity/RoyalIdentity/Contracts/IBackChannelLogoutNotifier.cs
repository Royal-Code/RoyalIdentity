using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contracts;

/// <summary>
/// The service responsible for performing back-channel logout notification.
/// </summary>
public interface IBackChannelLogoutNotifier
{
    /// <summary>
    /// Performs http back-channel logout notification.
    /// </summary>
    /// <param name="request">The model/request of the back channel logout notification.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(LogoutBackChannelRequest request, CancellationToken ct);
}
