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
    /// <returns></returns>
    Task<string> StoreAuthorizationCodeAsync(AuthorizationCode code);

    /// <summary>
    /// Gets the authorization code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns></returns>
    Task<AuthorizationCode> GetAuthorizationCodeAsync(string code);

    /// <summary>
    /// Removes the authorization code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns></returns>
    Task RemoveAuthorizationCodeAsync(string code);
}
