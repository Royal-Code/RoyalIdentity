using RoyalIdentity.Options;
using RoyalIdentity.Security.Keys;

namespace RoyalIdentity.Models.Keys;

/// <summary>
/// IdP-specific factory that validates the algorithm against realm <see cref="KeyOptions"/> and then
/// delegates material generation to the generic <see cref="KeyMaterialFactory"/>.
/// </summary>
public static class KeyParametersFactory
{
    public static RoyalIdentity.Security.Keys.KeyParameters Create(
        KeyOptions keyOptions,
        string? algorithm = null,
        TimeSpan? lifetime = null)
    {
        if (algorithm is not null && !keyOptions.SigningCredentialsAlgorithms.Contains(algorithm))
            throw new NotSupportedException($"The specified algorithm '{algorithm}' is not supported.");

        algorithm ??= keyOptions.MainSigningCredentialsAlgorithm;
        lifetime ??= keyOptions.DefaultSigningCredentialsLifetime;

        return KeyMaterialFactory.Create(algorithm, lifetime, keyOptions.RsaKeySizeInBytes);
    }
}
