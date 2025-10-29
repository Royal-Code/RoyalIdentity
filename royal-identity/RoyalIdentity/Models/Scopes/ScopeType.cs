namespace RoyalIdentity.Models.Resources;

/// <summary>
/// Defines the type of scope.
/// </summary>
public enum ScopeType
{
    /// <summary>
    /// Scope is an identity resource.
    /// This includes standard OpenID Connect scopes like "openid", "profile", and "email".
    /// </summary>
    Identity,

    /// <summary>
    /// Represents a group of API resources.
    /// This type is used to define a collection of APIs that can be accessed together.
    /// </summary>
    ResourceServer,

    /// <summary>
    /// Represents an individual API resource.
    /// This type is used to define specific APIs that clients can request access to.
    /// </summary>
    ApiResource,

    /// <summary>
    /// Represents an API scope.
    /// This type is used to define the permissions that a client can request for a specific API resource.
    /// For example, an API scope might represent read or write access to a particular API.
    /// </summary>
    ApiScope,
}
