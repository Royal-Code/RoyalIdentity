using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace RoyalIdentity.Security.Keys;

/// <summary>
/// Helpers to strip private key material from asymmetric <see cref="SecurityKey"/>s, keeping the
/// public key (and <see cref="SecurityKey.KeyId"/>) intact. Used when publishing public JWKs.
/// </summary>
public static class SecurityKeyExtensions
{
    public static RsaSecurityKey WithoutPrivateKey(this RsaSecurityKey key)
    {
        if (key.PrivateKeyStatus is not PrivateKeyStatus.Exists || key.Rsa is null)
        {
            return key;
        }

        var publicParameters = key.Rsa.ExportParameters(false);

        return new RsaSecurityKey(RSA.Create(publicParameters))
        {
            KeyId = key.KeyId,
            CryptoProviderFactory = key.CryptoProviderFactory,
        };
    }

    public static ECDsaSecurityKey WithoutPrivateKey(this ECDsaSecurityKey key)
    {
        if (key.ECDsa is null)
            return key;

        // ECDsaSecurityKey.PrivateKeyStatus returns Unknown on Linux even when the key has private
        // material, so we check directly by exporting and inspecting D.
        ECParameters withPrivate;
        try
        {
            withPrivate = key.ECDsa.ExportParameters(true);
        }
        catch (CryptographicException)
        {
            return key; // No private material — already public-only
        }

        if (withPrivate.D is null)
            return key;

        return new ECDsaSecurityKey(ECDsa.Create(key.ECDsa.ExportParameters(false)))
        {
            KeyId = key.KeyId,
            CryptoProviderFactory = key.CryptoProviderFactory,
        };
    }
}
