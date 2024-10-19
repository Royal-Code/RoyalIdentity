using System.Collections.Specialized;

namespace RoyalIdentity.Contracts.Storage;

public interface IAuthorizeParametersStore
{
    /// <summary>
    /// Writes the authorization parameters.
    /// </summary>
    /// <param name="parameters">The request authorize endpoint parameters.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>The identifier for the stored message.</returns>
    Task<string> WriteAsync(NameValueCollection parameters, CancellationToken ct);

    /// <summary>
    /// Reads the authorization parameters.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns></returns>
    Task<NameValueCollection?> ReadAsync(string id, CancellationToken ct);

    /// <summary>
    /// Deletes the authorization parameters.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns></returns>
    Task DeleteAsync(string id, CancellationToken ct);
}
