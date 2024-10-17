using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Storage;

public interface IClientStore
{
    /// <summary>
    /// Finds a client by id
    /// </summary>
    /// <param name="clientId">The client id</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>The client</returns>
    Task<Client> FindClientByIdAsync(string clientId, CancellationToken ct);

    /// <summary>
    /// Finds a enabled client by id
    /// </summary>
    /// <param name="clientId">The client id</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>The client</returns>
    Task<Client> FindEnabledClientByIdAsync(string clientId, CancellationToken ct);
}
