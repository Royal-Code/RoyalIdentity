using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models.Keys;

namespace RoyalIdentity.Contracts;

public interface IKeyManager
{
    /// <summary>
    /// Gets all signing credentials.
    /// </summary>
    /// <returns></returns>
    ValueTask<IReadOnlyList<SigningCredentials>> GetAllSigningCredentialsAsync(CancellationToken ct);

    ValueTask<SigningCredentials?> GetSigningCredentialsAsync(
        ICollection<string> allowedIdentityTokenSigningAlgorithms, 
        CancellationToken ct);

    ValueTask<SigningCredentials?> GetSigningCredentialsAsync(CancellationToken ct);

    /// <summary>
    /// Gets all validation keys.
    /// </summary>
    /// <returns></returns>
    ValueTask<ValidationKeysInfo> GetValidationKeysAsync(CancellationToken ct);

    /// <summary>
    /// Creates a new <see cref="SigningCredentials" /> for the current algorithm.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<SigningCredentials> CreateSigningCredentialsAsync(CancellationToken ct);
}
