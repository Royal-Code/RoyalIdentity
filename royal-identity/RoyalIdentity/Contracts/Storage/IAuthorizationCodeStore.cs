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
    /// Consumes the code atomically when its client and redirect URI match. At most one concurrent caller
    /// receives the code; absent, already-consumed and mismatched codes all return <c>null</c>, and a mismatch
    /// does not consume the code.
    /// </summary>
    /// <param name="code">The raw authorization-code handle.</param>
    /// <param name="clientId">The expected client identifier (Ordinal).</param>
    /// <param name="redirectUri">The expected redirect URI (Ordinal).</param>
    /// <param name="ct">The cancellation token.</param>
    Task<AuthorizationCode?> ConsumeAuthorizationCodeAsync(
        string code, string clientId, string redirectUri, CancellationToken ct);

    /// <summary>
    /// Removes the authorization code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveAuthorizationCodeAsync(string code, CancellationToken ct);
}
