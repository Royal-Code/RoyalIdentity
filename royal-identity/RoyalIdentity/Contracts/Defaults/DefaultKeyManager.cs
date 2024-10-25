using Microsoft.IdentityModel.Tokens;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultKeyManager : IKeyManager
{
    public async Task<SigningCredentials?> GetSigningCredentialsAsync(
        ICollection<string> allowedIdentityTokenSigningAlgorithms, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return null;
    }
}
