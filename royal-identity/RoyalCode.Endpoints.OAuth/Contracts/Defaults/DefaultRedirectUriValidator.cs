using RoyalIdentity.Extensions;
using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultRedirectUriValidator : IRedirectUriValidator
{
    /// <summary>
    /// Checks if a given URI string is in a collection of strings (using ordinal ignore case comparison).
    /// Wildcards is allowed for the uris.
    /// </summary>
    /// <param name="uris">The uris.</param>
    /// <param name="requestedUri">The requested URI.</param>
    /// <returns>
    ///     True if requested uri is in the collection; false otherwise.
    /// </returns>
    public static bool MatchRedirectUri(IEnumerable<string> uris, string requestedUri)
    {
        if (uris is null)
            return false;

        foreach (var s in uris)
        {
            // if has wildcard, try to match, if not, compare normally
            if ((s.HasWildcard() && s.MatchWildcard(requestedUri))
                || s.Equals(requestedUri, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Determines whether a redirect URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns>
    ///   <c>true</c> is the URI is valid; <c>false</c> otherwise.
    /// </returns>
    public virtual ValueTask<bool> IsRedirectUriValidAsync(string requestedUri, Client client)
    {
        return ValueTask.FromResult(MatchRedirectUri(client.RedirectUris, requestedUri));
    }

    /// <summary>
    /// Determines whether a post logout URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns>
    ///   <c>true</c> is the URI is valid; <c>false</c> otherwise.
    /// </returns>
    public virtual ValueTask<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client)
    {
        return ValueTask.FromResult(MatchRedirectUri(client.PostLogoutRedirectUris, requestedUri));
    }
}
