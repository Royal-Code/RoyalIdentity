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
    /// <returns>
    /// Boolean if consent is required.
    /// </returns>
    ValueTask<bool> RequiresConsentAsync(ClaimsPrincipal subject, Client client, Resources resources);

    /// <summary>
    /// Updates the consent.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="client">The client.</param>
    /// <param name="parsedScopes">The scopes.</param>
    /// <returns></returns>
    Task UpdateConsentAsync(ClaimsPrincipal subject, Client client, IEnumerable<string> scopes);
}
