using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultRedirectUriValidator : IRedirectUriValidator
{
    /// <summary>
    /// Checks whether a safe URI is in a collection using the effective realm policy.
    /// </summary>
    /// <param name="uris">The uris.</param>
    /// <param name="requestedUri">The requested URI.</param>
    /// <returns>
    ///     True if requested uri is in the collection; false otherwise.
    /// </returns>
    public static bool MatchRedirectUri(
        IEnumerable<string>? uris,
        string requestedUri,
        RedirectUriValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (uris is null || !IsSafeRequestedUri(requestedUri))
            return false;

        var comparison = options.Comparison switch
        {
            RedirectUriComparison.Ordinal => StringComparison.Ordinal,
            RedirectUriComparison.OrdinalIgnoreCase => StringComparison.OrdinalIgnoreCase,
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Comparison, null),
        };

        foreach (var registeredUri in uris)
        {
            if (options.ValidateRegisteredUri(registeredUri).Count is not 0)
                continue;

            if ((options.AllowWildcard
                    && registeredUri.HasWildcard()
                    && registeredUri.MatchWildcard(requestedUri, comparison))
                || registeredUri.Equals(requestedUri, comparison))
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
    public virtual ValueTask<bool> IsRedirectUriValidAsync(
        string requestedUri,
        Client client,
        RedirectUriValidationOptions options,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(MatchRedirectUri(client.RedirectUris, requestedUri, options));
    }

    /// <summary>
    /// Determines whether a post logout URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns>
    ///   <c>true</c> is the URI is valid; <c>false</c> otherwise.
    /// </returns>
    public virtual ValueTask<bool> IsPostLogoutRedirectUriValidAsync(
        string requestedUri,
        Client client,
        RedirectUriValidationOptions options,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(MatchRedirectUri(client.PostLogoutRedirectUris, requestedUri, options));
    }

    private static bool IsSafeRequestedUri(string? requestedUri)
        => !string.IsNullOrWhiteSpace(requestedUri)
            && !requestedUri.Contains('*', StringComparison.Ordinal)
            && Uri.TryCreate(requestedUri, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(uri.Fragment);
}
