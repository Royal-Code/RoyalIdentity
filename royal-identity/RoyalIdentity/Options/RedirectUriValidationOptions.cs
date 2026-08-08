using RoyalIdentity.Extensions;

namespace RoyalIdentity.Options;

/// <summary>
/// Realm-scoped policy for authorization and post-logout redirect URI validation.
/// </summary>
public class RedirectUriValidationOptions
{
    /// <summary>Creates the secure default policy.</summary>
    public RedirectUriValidationOptions()
    {
    }

    /// <summary>Creates an independent copy of another policy.</summary>
    public RedirectUriValidationOptions(RedirectUriValidationOptions other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Comparison = other.Comparison;
        AllowWildcard = other.AllowWildcard;
    }

    /// <summary>
    /// Gets or sets the complete-string comparison. The default is case-sensitive ordinal comparison.
    /// </summary>
    public RedirectUriComparison Comparison { get; set; } = RedirectUriComparison.Ordinal;

    /// <summary>
    /// Gets or sets whether a registered, structurally safe wildcard pattern may match a request.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool AllowWildcard { get; set; }

    /// <summary>Validates the policy itself.</summary>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (!Enum.IsDefined(Comparison))
            errors.Add("RedirectUriValidation.Comparison must be Ordinal or OrdinalIgnoreCase.");

        return errors;
    }

    /// <summary>
    /// Validates one registered URI or wildcard pattern. Registration remains HTTPS-only in the current product
    /// scope; native-app custom schemes and HTTP loopback redirects require an explicit future capability.
    /// </summary>
    public IReadOnlyList<string> ValidateRegisteredUri(string? registeredUri)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(registeredUri))
        {
            errors.Add("A registered redirect URI must not be empty.");
            return errors;
        }

        if (registeredUri.Contains("*:/", StringComparison.Ordinal))
        {
            errors.Add("A registered redirect URI cannot wildcard its scheme.");
            return errors;
        }

        var expanded = registeredUri.ReplaceWildcard();
        if (expanded.Contains('*', StringComparison.Ordinal))
        {
            errors.Add("A registered redirect URI contains an unsupported wildcard pattern.");
            return errors;
        }

        var authorityStart = registeredUri.IndexOf("://", StringComparison.Ordinal);
        if (authorityStart >= 0)
        {
            authorityStart += 3;
            var authorityEnd = registeredUri.IndexOf('/', authorityStart);
            var authority = authorityEnd < 0
                ? registeredUri[authorityStart..]
                : registeredUri[authorityStart..authorityEnd];
            var host = authority.Split(':', 2)[0];
            if (host.Contains('*', StringComparison.Ordinal)
                && !host.StartsWith("*.", StringComparison.Ordinal))
            {
                errors.Add("A host wildcard must be a bounded left-most '*.' label.");
                return errors;
            }
        }

        if (!Uri.TryCreate(expanded, UriKind.Absolute, out var uri))
        {
            errors.Add("A registered redirect URI must be absolute.");
            return errors;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            errors.Add("A registered redirect URI must use HTTPS.");

        if (!string.IsNullOrEmpty(uri.Fragment))
            errors.Add("A registered redirect URI must not contain a fragment.");

        if (registeredUri.Contains("://*.", StringComparison.Ordinal)
            && !HasFixedWildcardHostSuffix(uri.Host))
        {
            errors.Add("A wildcard host must retain a fixed multi-label suffix.");
        }

        return errors;
    }

    private static bool HasFixedWildcardHostSuffix(string expandedHost)
    {
        const string placeholder = "wildcard.";
        if (!expandedHost.StartsWith(placeholder, StringComparison.OrdinalIgnoreCase))
            return false;

        var suffix = expandedHost[placeholder.Length..];
        return suffix.Contains('.')
            && !suffix.StartsWith(".", StringComparison.Ordinal)
            && !suffix.EndsWith(".", StringComparison.Ordinal);
    }
}
