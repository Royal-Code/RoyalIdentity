
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Models.Keys;

namespace RoyalIdentity.Contracts;

public interface IKeyManager
{
    [Redesign("Verificar, futuramente, se pode ser ValueTask")]
    ValueTask<SigningCredentials?> GetSigningCredentialsAsync(
        ICollection<string> allowedIdentityTokenSigningAlgorithms, 
        CancellationToken ct);

    /// <summary>
    /// Gets all validation keys.
    /// </summary>
    /// <returns></returns>
    ValueTask<IReadOnlyList<SecurityKeyInfo>> GetValidationKeysAsync(CancellationToken ct);

    /// <summary>
    /// Gets all signing credentials.
    /// </summary>
    /// <returns></returns>
    ValueTask<IReadOnlyList<SigningCredentials>> GetAllSigningCredentialsAsync(CancellationToken ct);
}
