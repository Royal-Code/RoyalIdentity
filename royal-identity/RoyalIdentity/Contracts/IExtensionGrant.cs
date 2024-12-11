using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts;

/// <summary>
/// Represents an extension grant.
/// </summary>
public interface IExtensionGrant
{
    /// <summary>
    /// Creates the context for the specified grant type.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>
    /// A <see cref="ValueTask" /> representing the asynchronous operation.
    /// </returns>
    ValueTask<ITokenEndpointContextBase> CreateContextAsync(CancellationToken ct);

    /// <summary>
    /// Returns the grant type this validator can deal with
    /// </summary>
    /// <value>
    /// The type of the grant.
    /// </value>
    string GrantType { get; }
}