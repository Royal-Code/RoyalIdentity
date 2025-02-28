using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Storage.InMemory;

public sealed class StorageProvider : IStorageProvider, IStorageSession    
{
    private readonly MemoryStorage storage;

    public StorageProvider(MemoryStorage storage)
    {
        this.storage = storage;
    }

    public IStorageSession CreateSession() => this;

    public void Dispose()
    {
        // Nothing to dispose
    }

    public IStorage GetStorage() => storage;
}
