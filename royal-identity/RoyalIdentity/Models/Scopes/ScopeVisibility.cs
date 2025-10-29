namespace RoyalIdentity.Models.Scopes;

/// <summary>
/// Establishes the visibility of a scope for discovery and consent purposes.
/// When a scope is Public, it will be shown in the discovery document and any client can request it.
/// When a scope is Internal, it will be hidden from the discovery document and can only be requested 
/// by clients that are explicitly allowed to use it.
/// </summary>
public enum ScopeVisibility
{
    /// <summary>
    /// A scope that is publicly visible and can be requested by any client.
    /// </summary>
    Public,

    /// <summary>
    /// A scope that is internal and can only be requested by specific clients.
    /// </summary>
    Internal,
}
