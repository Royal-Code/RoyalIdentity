using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultKeyManager : IKeyManager
{
    public async ValueTask<SigningCredentials?> GetSigningCredentialsAsync(
        ICollection<string> allowedIdentityTokenSigningAlgorithms, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return null;
    }

    public ValueTask<IReadOnlyList<SecurityKeyInfo>> GetValidationKeysAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<IReadOnlyList<SigningCredentials>> GetAllSigningCredentialsAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
