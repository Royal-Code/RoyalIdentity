using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Security.Keys;
using System.Security.Cryptography;

namespace Tests.Security.Keys;

public class SecurityKeyExtensionsTests
{
    [Fact]
    public void Rsa_WithoutPrivateKey_Removes_Private_Material_And_Preserves_KeyId()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "rsa-kid" };

        Assert.Equal(PrivateKeyStatus.Exists, key.PrivateKeyStatus);

        var publicKey = key.WithoutPrivateKey();

        Assert.Equal("rsa-kid", publicKey.KeyId);
        Assert.NotEqual(PrivateKeyStatus.Exists, publicKey.PrivateKeyStatus);

        // The public modulus/exponent are preserved; the private exponent is gone.
        var publicParams = publicKey.Rsa!.ExportParameters(false);
        Assert.Equal(rsa.ExportParameters(false).Modulus, publicParams.Modulus);
        Assert.Throws<CryptographicException>(() => publicKey.Rsa.ExportParameters(true));
    }

    [Fact]
    public void ECDsa_WithoutPrivateKey_Removes_Private_Material_And_Preserves_KeyId()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var key = new ECDsaSecurityKey(ecdsa) { KeyId = "ec-kid" };

        // Verify private material exists directly — PrivateKeyStatus may return Unknown on Linux
        // even for keys that have private material, so we don't rely on it here.
        Assert.NotNull(ecdsa.ExportParameters(true).D);

        var publicKey = key.WithoutPrivateKey();

        Assert.Equal("ec-kid", publicKey.KeyId);
        Assert.Throws<CryptographicException>(() => publicKey.ECDsa!.ExportParameters(true));
    }

    [Fact]
    public void Rsa_WithoutPrivateKey_Is_NoOp_When_Already_Public()
    {
        using var rsa = RSA.Create(2048);
        using var publicOnly = RSA.Create(rsa.ExportParameters(false));
        var key = new RsaSecurityKey(publicOnly) { KeyId = "kid" };

        var result = key.WithoutPrivateKey();

        Assert.Same(key, result);
    }
}
