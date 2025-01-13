using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Withs;

/// <summary>
/// Used for contexts that have clients and redirection uri.
/// </summary>
public interface IWithRedirectUri : IWithClient
{
    /// <summary>
    /// The <c>redirect_uri</c> parameter from the request.
    /// </summary>
    public string? RedirectUri { get; }

    /// <summary>
    /// Determines that <see cref=“RedirectUri”/> is valid for the client of the request.
    /// </summary>
    public void RedirectUriValidated();

    /// <summary>
    /// Ensure that there is a <see cref=“RedirectUri”/> and that it is valid for the client of the request.
    /// </summary>
    [MemberNotNull(nameof(RedirectUri))]
    public void AssertHasRedirectUri();
}
