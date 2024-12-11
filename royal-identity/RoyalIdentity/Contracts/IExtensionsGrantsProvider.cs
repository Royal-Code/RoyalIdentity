using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts;

public interface IExtensionsGrantsProvider
{
    /// <summary>
    /// Gets the available grant types.
    /// </summary>
    IReadOnlyList<string> GetAvailableGrantTypes();

    /// <summary>
    /// Creates the context for the specified grant type.
    /// </summary>
    /// <param name="grantType">The grant type.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The context.</returns>
    ValueTask<ITokenEndpointContextBase> CreateContextAsync(string grantType, CancellationToken ct);
}