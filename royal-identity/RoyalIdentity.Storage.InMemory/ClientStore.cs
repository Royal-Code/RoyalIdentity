using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using System.Collections.Concurrent;

namespace RoyalIdentity.Storage.InMemory;

public class ClientStore : IClientStore
{
    private readonly ConcurrentDictionary<string, Client> clients;

    public ClientStore(ConcurrentDictionary<string, Client> clients)
    {
        this.clients = clients;
    }

    public Task<Client?> FindClientByIdAsync(string clientId, CancellationToken ct)
    {
        clients.TryGetValue(clientId, out var client);
        return Task.FromResult(client);
    }

    public Task<Client?> FindEnabledClientByIdAsync(string clientId, CancellationToken ct)
    {
        clients.TryGetValue(clientId, out var client);
        return Task.FromResult(client?.Enabled is true ? client : null);
    }
}