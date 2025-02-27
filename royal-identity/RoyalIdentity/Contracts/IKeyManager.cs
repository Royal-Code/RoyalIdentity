using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Keys;

namespace RoyalIdentity.Contracts;

public interface IKeyManager
{
    /// <summary>
    /// Gets all signing credentials.
    /// </summary>
    /// <returns></returns>
    ValueTask<IReadOnlyList<SigningCredentials>> GetAllSigningCredentialsAsync(Realm realm, CancellationToken ct);

    ValueTask<SigningCredentials?> GetSigningCredentialsAsync(
        Realm realm,
        ICollection<string> allowedIdentityTokenSigningAlgorithms, 
        CancellationToken ct);

    ValueTask<SigningCredentials?> GetSigningCredentialsAsync(Realm realm, CancellationToken ct);

    /// <summary>
    /// Gets all validation keys.
    /// </summary>
    /// <returns></returns>
    ValueTask<ValidationKeysInfo> GetValidationKeysAsync(Realm realm, CancellationToken ct);

    /// <summary>
    /// Creates a new <see cref="SigningCredentials" /> for the current algorithm.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<SigningCredentials> CreateSigningCredentialsAsync(Realm realm, CancellationToken ct);
}
