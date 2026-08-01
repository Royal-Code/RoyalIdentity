// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts;

public interface IRedirectUriValidator
{
    /// <summary>
    /// Determines whether a redirect URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns><c>true</c> is the URI is valid; <c>false</c> otherwise.</returns>
    ValueTask<bool> IsRedirectUriValidAsync(string requestedUri, Client client);

    /// <summary>
    /// Determines whether a post logout URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns><c>true</c> is the URI is valid; <c>false</c> otherwise.</returns>
    ValueTask<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client);
}
