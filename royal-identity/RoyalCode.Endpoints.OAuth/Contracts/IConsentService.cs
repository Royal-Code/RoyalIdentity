using RoyalIdentity.Models;
using System.Security.Claims;

namespace RoyalIdentity.Contracts;

/// <summary>
/// Service to retrieve and update consent.
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Checks if consent is required.
    /// </summary>
    /// <param name="subject">The user.</param>
    /// <param name="client">The client.</param>
    /// <param name="resources">The scopes.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// Boolean if consent is required.
    /// </returns>
    ValueTask<bool> RequiresConsentAsync(ClaimsPrincipal subject, Client client, Resources resources, CancellationToken ct);

    /// <summary>
    /// Updates the consent.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="client">The client.</param>
    /// <param name="scopes">The scopes.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task UpdateConsentAsync(ClaimsPrincipal subject, Client client, IEnumerable<string> scopes, CancellationToken ct);
}
