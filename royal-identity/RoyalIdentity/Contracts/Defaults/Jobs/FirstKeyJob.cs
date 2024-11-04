
namespace RoyalIdentity.Contracts.Defaults.Jobs;

/// <summary>
/// Job for creating the first key, or a valid key for signatures.
/// </summary>
public class FirstKeyJob : IServerJob
{
    private readonly IKeyManager keyManager;

    public FirstKeyJob(IKeyManager keyManager)
    {
        this.keyManager = keyManager;
    }

    public string Name => nameof(FirstKeyJob);

    public bool Background => false;

    public async Task RunAsync(CancellationToken ct)
    {
        var key = await keyManager.GetSigningCredentialsAsync(ct);

        if (key is not null)
            return;

        keyManager.CreateSigningCredentialsAsync(ct);
    }
}
