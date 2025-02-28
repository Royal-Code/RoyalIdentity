using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Contracts.Defaults.Jobs;

/// <summary>
/// Job for creating the first key, or a valid key for signatures.
/// </summary>
public class FirstKeyJob : IServerJob
{
    private readonly IKeyManager keyManager;
    private readonly IStorage storage;

    public FirstKeyJob(IKeyManager keyManager, IStorage storage)
    {
        this.keyManager = keyManager;
        this.storage = storage;
    }

    public string Name => nameof(FirstKeyJob);

    public bool Background => false;

    public async Task RunAsync(CancellationToken ct)
    {
        await foreach (var realms in storage.Realms.GetAllAsync(ct))
        {
            var key = await keyManager.GetSigningCredentialsAsync(realms, ct);

            if (key is not null)
                return;

            await keyManager.CreateSigningCredentialsAsync(realms, ct);
        }
    }
}
