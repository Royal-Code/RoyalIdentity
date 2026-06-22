using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Security.Keys;
using System.Security.Cryptography;

namespace Tests.Security.Keys;

public class KeyMaterialFactoryTests
{
    [Theory]
    [InlineData(SecurityAlgorithms.RsaSha256)]
    [InlineData(SecurityAlgorithms.RsaSha384)]
    [InlineData(SecurityAlgorithms.RsaSha512)]
    [InlineData(SecurityAlgorithms.RsaSsaPssSha256)]
    public void Create_Rsa_Produces_Usable_Signing_Key(string algorithm)
    {
        var keyParameters = KeyMaterialFactory.Create(algorithm);

        Assert.Equal(algorithm, keyParameters.SecurityAlgorithm);
        Assert.Equal(KeySerializationFormat.Xml, keyParameters.Format);

        var rsaKey = keyParameters.CreateRsaSecurityKey();
        Assert.Equal(PrivateKeyStatus.Exists, rsaKey.PrivateKeyStatus);

        var data = System.Text.Encoding.UTF8.GetBytes("data");
        var sig = rsaKey.Rsa!.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.True(rsaKey.Rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Theory]
    [InlineData(SecurityAlgorithms.EcdsaSha256)]
    [InlineData(SecurityAlgorithms.EcdsaSha384)]
    [InlineData(SecurityAlgorithms.EcdsaSha512)]
    public void Create_ECDsa_Produces_Usable_Signing_Key(string algorithm)
    {
        var keyParameters = KeyMaterialFactory.Create(algorithm);

        Assert.Equal(algorithm, keyParameters.SecurityAlgorithm);
        Assert.Equal(KeySerializationFormat.Xml, keyParameters.Format);

        var ecKey = keyParameters.CreateECDsaSecurityKey();
        Assert.NotNull(ecKey.ECDsa);

        var data = System.Text.Encoding.UTF8.GetBytes("data");
        var sig = ecKey.ECDsa!.SignData(data, HashAlgorithmName.SHA256);
        Assert.True(ecKey.ECDsa.VerifyData(data, sig, HashAlgorithmName.SHA256));
    }

    [Theory]
    [InlineData(SecurityAlgorithms.HmacSha256, 32)]
    [InlineData(SecurityAlgorithms.HmacSha384, 48)]
    [InlineData(SecurityAlgorithms.HmacSha512, 64)]
    public void Create_Hmac_Produces_Algorithm_Appropriate_Signing_Key(string algorithm, int expectedKeySizeInBytes)
    {
        // Regression: the original core factory marked HMAC keys as KeyEncoding.Plain while storing a
        // Base64 value, which made CreateSymmetricSecurityKey throw. The generic factory fixes this by
        // marking the encoding Base64 and sizing key material according to the HMAC algorithm.
        var keyParameters = KeyMaterialFactory.Create(algorithm);

        Assert.Equal(KeySerializationFormat.None, keyParameters.Format);
        Assert.Equal(KeyEncoding.Base64, keyParameters.Encoding);

        var symmetricKey = keyParameters.CreateSymmetricSecurityKey();
        Assert.Equal(expectedKeySizeInBytes, symmetricKey.Key.Length);

        var data = System.Text.Encoding.UTF8.GetBytes("hmac-data");
        using var signer = new SymmetricSignatureProvider(symmetricKey, algorithm);
        var signature = signer.Sign(data);

        using var verifier = new SymmetricSignatureProvider(symmetricKey, algorithm);
        Assert.True(verifier.Verify(data, signature));
    }

    [Fact]
    public void Create_With_Lifetime_Sets_NotBefore_And_Expires()
    {
        var lifetime = TimeSpan.FromDays(30);

        var keyParameters = KeyMaterialFactory.Create(SecurityAlgorithms.RsaSha256, lifetime);

        Assert.NotNull(keyParameters.NotBefore);
        Assert.NotNull(keyParameters.Expires);
        Assert.Equal(keyParameters.Created, keyParameters.NotBefore);
        Assert.Equal(keyParameters.Created.Add(lifetime), keyParameters.Expires);
    }

    [Fact]
    public void Create_Without_Lifetime_Leaves_NotBefore_And_Expires_Null()
    {
        var keyParameters = KeyMaterialFactory.Create(SecurityAlgorithms.RsaSha256);

        Assert.Null(keyParameters.NotBefore);
        Assert.Null(keyParameters.Expires);
    }

    [Fact]
    public void Create_Generates_Unique_KeyIds()
    {
        var a = KeyMaterialFactory.Create(SecurityAlgorithms.EcdsaSha256);
        var b = KeyMaterialFactory.Create(SecurityAlgorithms.EcdsaSha256);

        Assert.NotEqual(a.KeyId, b.KeyId);
    }

    [Fact]
    public void Create_Throws_For_Unsupported_Algorithm()
    {
        Assert.Throws<NotSupportedException>(() => KeyMaterialFactory.Create("FOO256"));
    }

    [Fact]
    public void Create_Throws_For_Null_Or_Empty_Algorithm()
    {
        Assert.Throws<ArgumentNullException>(() => KeyMaterialFactory.Create(null!));
        Assert.Throws<ArgumentException>(() => KeyMaterialFactory.Create(""));
    }
}
