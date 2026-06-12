namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Defines the type of scope.
/// </summary>
public enum ScopeType
{
    /// <summary>
    /// An identity scope: standard OpenID Connect scopes like "openid", "profile", "email".
    /// </summary>
    Identity,

    /// <summary>
    /// A resource server (a Web API) that exposes protected scopes.
    /// </summary>
    ResourceServer,

    /// <summary>
    /// A scope: an operation exposed by a resource server.
    /// </summary>
    Scope,
}
