using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models.Keys;
using RoyalIdentity.Utils;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

namespace Tests.Identity.Keys;

public class KeyParametersTests
{
    [Fact]
    public void CreateRsaSecurityKey_ShouldCreateKeyCorrectly()
    {
        // Arrange
        var rsa = RSA.Create();
        var rsaParameters = rsa.ExportParameters(true);

        var rsaKeyXml = SerializeToXml(rsaParameters);
        var keyParameters = new KeyParameters(
            "key-id",
            "RSA Key",
            "RS256",
            KeySerializationFormat.Xml,
            KeyEncoding.Plain,
            rsaKeyXml);

        // Act
        var rsaSecurityKey = keyParameters.CreateRsaSecurityKey();

        // Assert
        Assert.NotNull(rsaSecurityKey);
        Assert.True(rsaSecurityKey.CanComputeJwkThumbprint());
        Assert.Equal(PrivateKeyStatus.Exists, rsaSecurityKey.PrivateKeyStatus);
        Assert.NotNull(rsaSecurityKey.Rsa);

        var newParameters = rsaSecurityKey.Rsa.ExportParameters(true);
        Assert.Equal(rsaParameters.D, newParameters.D);
        Assert.Equal(rsaParameters.DP, newParameters.DP);
        Assert.Equal(rsaParameters.DQ, newParameters.DQ);
        Assert.Equal(rsaParameters.Exponent, newParameters.Exponent);
        Assert.Equal(rsaParameters.InverseQ, newParameters.InverseQ);
        Assert.Equal(rsaParameters.Modulus, newParameters.Modulus);
        Assert.Equal(rsaParameters.P, newParameters.P);
        Assert.Equal(rsaParameters.Q, newParameters.Q);
    }

    private static string SerializeToXml(RSAParameters rsaParameters)
    {
        using var stringWriter = new StringWriter();
        var xmlSerializer = new XmlSerializer(typeof(RSAParameters));
        xmlSerializer.Serialize(stringWriter, rsaParameters);
        return stringWriter.ToString();
    }

    [Fact]
    public void CreateECDsaSecurityKey_ShouldCreateKeyCorrectly()
    {
        // Arrange
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var ecKeyXml = ECKeyHelper.ExportECParametersToXml(ecdsa, true);
        var keyParameters = new KeyParameters(
            "key-id",
            "ECDsa Key",
            "ES256",
            KeySerializationFormat.Xml,
            KeyEncoding.Plain,
            ecKeyXml);

        // Act
        var ecdsaSecurityKey = keyParameters.CreateECDsaSecurityKey();

        // Assert
        Assert.NotNull(ecdsaSecurityKey);
        Assert.True(ecdsaSecurityKey.CanComputeJwkThumbprint());
        Assert.NotEqual(PrivateKeyStatus.DoesNotExist, ecdsaSecurityKey.PrivateKeyStatus);
        Assert.NotNull(ecdsaSecurityKey.ECDsa);
    }

    [Fact]
    public void CreateSymmetricSecurityKey_ShouldCreateKeyCorrectly()
    {
        // Arrange
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        var keyBase64 = Convert.ToBase64String(keyBytes);

        var keyParameters = new KeyParameters(
            "key-id",
            "Symmetric Key",
            "HS256",
            KeySerializationFormat.Json, // format is not used here
            KeyEncoding.Base64,
            keyBase64);

        // Act
        var symmetricSecurityKey = keyParameters.CreateSymmetricSecurityKey();

        // Assert
        Assert.NotNull(symmetricSecurityKey);
        Assert.Equal(keyBytes, symmetricSecurityKey.Key);
    }

    [Fact]
    public void CreateRsaSecurityKey_ShouldSignAndVerifyCorrectly()
    {
        // Generate RSA key parameters
        using RSA rsa = RSA.Create(2048);
        RSAParameters rsaParams = rsa.ExportParameters(true);
        string serializedRsaParams = JsonSerializer.Serialize(rsaParams, new JsonSerializerOptions()
        {
            IncludeFields = true
        });

        // Create RSA Security Key from serialized parameters
        var keyParameters = new KeyParameters("keyId", "name", "RS256", KeySerializationFormat.Json, KeyEncoding.Plain, serializedRsaParams);
        RsaSecurityKey rsaKey = keyParameters.CreateRsaSecurityKey();

        // Sign some data
        byte[] data = Encoding.UTF8.GetBytes("Test Data");
        byte[] signature = rsaKey.Rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Deserialize another RSA key to verify the signature
        RsaSecurityKey verificationKey = keyParameters.CreateRsaSecurityKey();

        bool isVerified = verificationKey.Rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Assert that the signature is verified correctly
        Assert.True(isVerified);
    }

    [Fact]
    public void CreateECDsaSecurityKey_ShouldSignAndVerifyCorrectly()
    {
        // Generate ECDsa key parameters
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters ecParams = ecdsa.ExportParameters(true);
        string serializedEcParams = ECKeyHelper.SerializeECParameters(ecParams);

        // Create ECDsa Security Key from serialized parameters
        var keyParameters = new KeyParameters("keyId", "name", "ES256", KeySerializationFormat.Json, KeyEncoding.Plain, serializedEcParams);
        ECDsaSecurityKey ecdsaKey = keyParameters.CreateECDsaSecurityKey();

        // Sign some data
        byte[] data = Encoding.UTF8.GetBytes("Test Data");
        byte[] signature = ecdsaKey.ECDsa.SignData(data, HashAlgorithmName.SHA256);

        // Deserialize another ECDsa key to verify the signature
        ECDsaSecurityKey verificationKey = keyParameters.CreateECDsaSecurityKey();

        bool isVerified = verificationKey.ECDsa.VerifyData(data, signature, HashAlgorithmName.SHA256);

        // Assert that the signature is verified correctly
        Assert.True(isVerified);
    }

    [Fact]
    public void CreateSymmetricSecurityKey_ShouldSignAndVerifyCorrectly()
    {
        // Create a symmetric key
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        string base64Key = Convert.ToBase64String(key);

        var keyParameters = new KeyParameters("keyId", "name", "HS256", KeySerializationFormat.Json, KeyEncoding.Base64, base64Key);
        SymmetricSecurityKey symmetricKey = keyParameters.CreateSymmetricSecurityKey();

        // Sign some data using HMACSHA256
        byte[] data = Encoding.UTF8.GetBytes("Test Data");
        using var hmac = new HMACSHA256(symmetricKey.Key);
        byte[] signature = hmac.ComputeHash(data);

        // Deserialize another symmetric key to verify the signature
        SymmetricSecurityKey verificationKey = keyParameters.CreateSymmetricSecurityKey();

        // Verify the signature
        using var hmacVerification = new HMACSHA256(verificationKey.Key);
        byte[] verificationSignature = hmacVerification.ComputeHash(data);

        // Assert that the signatures match
        Assert.Equal(signature, verificationSignature);
    }
}
