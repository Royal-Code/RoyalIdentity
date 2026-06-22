using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Security.Keys;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

namespace Tests.Security.Keys;

public class KeyParametersTests
{
    private static string SerializeRsaToXml(RSAParameters rsaParameters)
    {
        using var stringWriter = new StringWriter();
        var xmlSerializer = new XmlSerializer(typeof(RSAParameters));
        xmlSerializer.Serialize(stringWriter, rsaParameters);
        return stringWriter.ToString();
    }

    private static string SerializeRsaToJson(RSAParameters rsaParameters)
        => JsonSerializer.Serialize(rsaParameters, new JsonSerializerOptions { IncludeFields = true });

    // --- RSA -----------------------------------------------------------------

    [Fact]
    public void Rsa_Xml_RoundTrip_Preserves_All_Parameters()
    {
        using var rsa = RSA.Create(2048);
        var rsaParameters = rsa.ExportParameters(true);

        var keyParameters = new KeyParameters(
            "key-id", "RSA Key", "RS256",
            KeySerializationFormat.Xml, KeyEncoding.Plain,
            SerializeRsaToXml(rsaParameters));

        var rsaSecurityKey = keyParameters.CreateRsaSecurityKey();

        Assert.NotNull(rsaSecurityKey);
        Assert.Equal("key-id", rsaSecurityKey.KeyId);
        Assert.True(rsaSecurityKey.CanComputeJwkThumbprint());
        Assert.Equal(PrivateKeyStatus.Exists, rsaSecurityKey.PrivateKeyStatus);
        Assert.NotNull(rsaSecurityKey.Rsa);

        var roundTripped = rsaSecurityKey.Rsa!.ExportParameters(true);
        Assert.Equal(rsaParameters.D, roundTripped.D);
        Assert.Equal(rsaParameters.DP, roundTripped.DP);
        Assert.Equal(rsaParameters.DQ, roundTripped.DQ);
        Assert.Equal(rsaParameters.Exponent, roundTripped.Exponent);
        Assert.Equal(rsaParameters.InverseQ, roundTripped.InverseQ);
        Assert.Equal(rsaParameters.Modulus, roundTripped.Modulus);
        Assert.Equal(rsaParameters.P, roundTripped.P);
        Assert.Equal(rsaParameters.Q, roundTripped.Q);
    }

    [Fact]
    public void Rsa_Json_RoundTrip_Preserves_Modulus()
    {
        using var rsa = RSA.Create(2048);
        var rsaParameters = rsa.ExportParameters(true);

        var keyParameters = new KeyParameters(
            "key-id", "RSA Key", "RS256",
            KeySerializationFormat.Json, KeyEncoding.Plain,
            SerializeRsaToJson(rsaParameters));

        var rsaSecurityKey = keyParameters.CreateRsaSecurityKey();

        Assert.NotNull(rsaSecurityKey.Rsa);
        Assert.Equal(rsaParameters.Modulus, rsaSecurityKey.Rsa!.ExportParameters(false).Modulus);
    }

    [Fact]
    public void Rsa_Signs_And_Verifies()
    {
        using var rsa = RSA.Create(2048);
        var serialized = SerializeRsaToJson(rsa.ExportParameters(true));

        var keyParameters = new KeyParameters(
            "keyId", "name", "RS256",
            KeySerializationFormat.Json, KeyEncoding.Plain, serialized);

        var signingKey = keyParameters.CreateRsaSecurityKey();
        var verificationKey = keyParameters.CreateRsaSecurityKey();

        var data = Encoding.UTF8.GetBytes("Test Data");
        var signature = signingKey.Rsa!.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        Assert.True(verificationKey.Rsa!.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    // --- ECDsa ---------------------------------------------------------------

    [Fact]
    public void ECDsa_Xml_RoundTrip_Creates_Key()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var ecKeyXml = ECKeyHelper.ExportECParametersToXml(ecdsa, true);

        var keyParameters = new KeyParameters(
            "key-id", "ECDsa Key", "ES256",
            KeySerializationFormat.Xml, KeyEncoding.Plain, ecKeyXml);

        var ecdsaSecurityKey = keyParameters.CreateECDsaSecurityKey();

        Assert.NotNull(ecdsaSecurityKey);
        Assert.Equal("key-id", ecdsaSecurityKey.KeyId);
        Assert.True(ecdsaSecurityKey.CanComputeJwkThumbprint());
        Assert.NotEqual(PrivateKeyStatus.DoesNotExist, ecdsaSecurityKey.PrivateKeyStatus);
        Assert.NotNull(ecdsaSecurityKey.ECDsa);
    }

    [Fact]
    public void ECDsa_Json_RoundTrip_Creates_Key()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var serialized = ECKeyHelper.SerializeECParameters(ecdsa.ExportParameters(true));

        var keyParameters = new KeyParameters(
            "key-id", "ECDsa Key", "ES256",
            KeySerializationFormat.Json, KeyEncoding.Plain, serialized);

        var ecdsaSecurityKey = keyParameters.CreateECDsaSecurityKey();

        Assert.NotNull(ecdsaSecurityKey.ECDsa);
        Assert.Equal("key-id", ecdsaSecurityKey.KeyId);
    }

    [Fact]
    public void ECDsa_Signs_And_Verifies()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var serialized = ECKeyHelper.SerializeECParameters(ecdsa.ExportParameters(true));

        var keyParameters = new KeyParameters(
            "keyId", "name", "ES256",
            KeySerializationFormat.Json, KeyEncoding.Plain, serialized);

        var signingKey = keyParameters.CreateECDsaSecurityKey();
        var verificationKey = keyParameters.CreateECDsaSecurityKey();

        var data = Encoding.UTF8.GetBytes("Test Data");
        var signature = signingKey.ECDsa!.SignData(data, HashAlgorithmName.SHA256);

        Assert.True(verificationKey.ECDsa!.VerifyData(data, signature, HashAlgorithmName.SHA256));
    }

    // --- Symmetric -----------------------------------------------------------

    [Fact]
    public void Symmetric_Base64_RoundTrip_Preserves_Bytes()
    {
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);

        var keyParameters = new KeyParameters(
            "key-id", "Symmetric Key", "HS256",
            KeySerializationFormat.None, KeyEncoding.Base64,
            Convert.ToBase64String(keyBytes));

        var symmetricSecurityKey = keyParameters.CreateSymmetricSecurityKey();

        Assert.NotNull(symmetricSecurityKey);
        Assert.Equal("key-id", symmetricSecurityKey.KeyId);
        Assert.Equal(keyBytes, symmetricSecurityKey.Key);
    }

    [Fact]
    public void Symmetric_Hex_RoundTrip_Preserves_Bytes()
    {
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);

        var keyParameters = new KeyParameters(
            "key-id", "Symmetric Key", "HS256",
            KeySerializationFormat.None, KeyEncoding.Hex,
            Convert.ToHexString(keyBytes));

        var symmetricSecurityKey = keyParameters.CreateSymmetricSecurityKey();

        Assert.Equal(keyBytes, symmetricSecurityKey.Key);
    }

    [Fact]
    public void Symmetric_Signs_And_Verifies()
    {
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);

        var keyParameters = new KeyParameters(
            "keyId", "name", "HS256",
            KeySerializationFormat.None, KeyEncoding.Base64,
            Convert.ToBase64String(keyBytes));

        var key = keyParameters.CreateSymmetricSecurityKey();

        var data = Encoding.UTF8.GetBytes("Test Data");
        using var hmac = new HMACSHA256(key.Key);
        var signature = hmac.ComputeHash(data);

        var verificationKey = keyParameters.CreateSymmetricSecurityKey();
        using var hmacVerify = new HMACSHA256(verificationKey.Key);
        Assert.Equal(signature, hmacVerify.ComputeHash(data));
    }

    // --- GetValidationKey ----------------------------------------------------

    [Fact]
    public void GetValidationKey_Rsa_Produces_Public_Jwk_Without_Private_Material()
    {
        using var rsa = RSA.Create(2048);
        var keyParameters = new KeyParameters(
            "rsa-kid", "name", "RS256",
            KeySerializationFormat.Json, KeyEncoding.Plain,
            SerializeRsaToJson(rsa.ExportParameters(true)));

        var (key, jwk) = keyParameters.GetValidationKey();

        Assert.NotNull(key);
        Assert.NotNull(jwk);
        Assert.Equal("rsa-kid", jwk!.KeyId);
        Assert.Equal("RS256", jwk.Alg);
        // public RSA JWK carries N and E, never the private exponent D.
        Assert.False(string.IsNullOrEmpty(jwk.N));
        Assert.False(string.IsNullOrEmpty(jwk.E));
        Assert.True(string.IsNullOrEmpty(jwk.D));
        Assert.True(string.IsNullOrEmpty(jwk.P));
        Assert.True(string.IsNullOrEmpty(jwk.Q));
    }

    [Fact]
    public void GetValidationKey_ECDsa_Produces_Public_Jwk_Without_Private_Material()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyParameters = new KeyParameters(
            "ec-kid", "name", "ES256",
            KeySerializationFormat.Json, KeyEncoding.Plain,
            ECKeyHelper.SerializeECParameters(ecdsa.ExportParameters(true)));

        var (key, jwk) = keyParameters.GetValidationKey();

        Assert.NotNull(key);
        Assert.NotNull(jwk);
        Assert.Equal("ec-kid", jwk!.KeyId);
        Assert.Equal("ES256", jwk.Alg);
        Assert.False(string.IsNullOrEmpty(jwk.X));
        Assert.False(string.IsNullOrEmpty(jwk.Y));
        Assert.True(string.IsNullOrEmpty(jwk.D));
    }

    [Fact]
    public void GetValidationKey_Symmetric_Returns_Key_And_Null_Jwk()
    {
        var keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        var keyParameters = new KeyParameters(
            "hs-kid", "name", "HS256",
            KeySerializationFormat.None, KeyEncoding.Base64,
            Convert.ToBase64String(keyBytes));

        var (key, jwk) = keyParameters.GetValidationKey();

        Assert.IsType<SymmetricSecurityKey>(key);
        Assert.Null(jwk);
    }

    // --- Failure modes -------------------------------------------------------

    [Fact]
    public void GetSecurityKey_Throws_For_Unsupported_Algorithm()
    {
        var keyParameters = new KeyParameters(
            "kid", "name", "NONE",
            KeySerializationFormat.None, KeyEncoding.Plain, "x");

        Assert.Throws<NotSupportedException>(() => keyParameters.GetSecurityKey());
    }

    [Fact]
    public void CreateRsaSecurityKey_Throws_When_Algorithm_Is_Not_Rsa()
    {
        var keyParameters = new KeyParameters(
            "kid", "name", "ES256",
            KeySerializationFormat.Json, KeyEncoding.Plain, "{}");

        Assert.Throws<InvalidOperationException>(() => keyParameters.CreateRsaSecurityKey());
    }
}
