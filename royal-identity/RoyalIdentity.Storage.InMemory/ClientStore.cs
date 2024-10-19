using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;

namespace RoyalIdentity.Storage.InMemory;

public class ClientStore : IClientStore
{
    private readonly MemoryStorage storage;

    public ClientStore(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public Task<Client?> FindClientByIdAsync(string clientId, CancellationToken ct)
    {
        storage.Clients.TryGetValue(clientId, out var client);
        return Task.FromResult(client);
    }

    public Task<Client?> FindEnabledClientByIdAsync(string clientId, CancellationToken ct)
    {
        storage.Clients.TryGetValue(clientId, out var client);
        return Task.FromResult(client?.Enabled is true ? client : null);
    }
}