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

}
