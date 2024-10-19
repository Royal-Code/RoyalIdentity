using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Interface for the authorization code store
/// </summary>
public interface IAuthorizationCodeStore
{
    /// <summary>
    /// Stores the authorization code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<string> StoreAuthorizationCodeAsync(AuthorizationCode code, CancellationToken ct);

    /// <summary>
    /// Gets the authorization code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<AuthorizationCode?> GetAuthorizationCodeAsync(string code, CancellationToken ct);

    /// <summary>
    /// Removes the authorization code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveAuthorizationCodeAsync(string code, CancellationToken ct);
}
