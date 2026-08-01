// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
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
    ValueTask<bool> RequiresConsentAsync(ClaimsPrincipal subject, Client client, RequestedResources resources, CancellationToken ct);

    /// <summary>
    /// Updates the consent.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="client">The client.</param>
    /// <param name="scopes">The scopes.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task UpdateConsentAsync(ClaimsPrincipal subject, Client client, IEnumerable<ConsentedScope> scopes, CancellationToken ct);

    /// <summary>
    /// Validates if consent is valid.
    /// </summary>
    /// <param name="subject">The user.</param>
    /// <param name="client">The client.</param>
    /// <param name="resources">The scopes.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// Boolean if the resources are consented.
    /// </returns>
    ValueTask<bool> ValidateConsentAsync(ClaimsPrincipal subject, Client client, RequestedResources resources, CancellationToken ct);
}
