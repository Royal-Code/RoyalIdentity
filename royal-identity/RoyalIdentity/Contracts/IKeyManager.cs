
using Microsoft.IdentityModel.Tokens;

namespace RoyalIdentity.Contracts;

public interface IKeyManager
{
    [Redesign("Verificar, futuramente, se pode ser ValueTask")]
    Task<SigningCredentials?> GetSigningCredentialsAsync(
        ICollection<string> allowedIdentityTokenSigningAlgorithms, 
        CancellationToken ct);
}
