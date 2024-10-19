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
