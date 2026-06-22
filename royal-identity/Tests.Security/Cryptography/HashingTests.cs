using System.Security.Cryptography;
using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.Security.Encoding;

namespace Tests.Security.Cryptography;

public class HashingTests
{
    [Fact]
    public void Sha256_Matches_Known_Vector()
    {
        var hex = Convert.ToHexString(Hashing.Sha256("abc"u8)).ToLowerInvariant();

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hex);
    }

    [Fact]
    public void Sha384_Matches_Known_Vector()
    {
        var hex = Convert.ToHexString(Hashing.Sha384("abc"u8)).ToLowerInvariant();

        Assert.Equal(
            "cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7",
            hex);
    }

    [Fact]
    public void Sha512_Matches_Known_Vector()
    {
        var hex = Convert.ToHexString(Hashing.Sha512("abc"u8)).ToLowerInvariant();

        Assert.Equal(
            "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f",
            hex);
    }

    [Fact]
    public void Extensions_And_Base64_Helpers_Match_Legacy_Format()
    {
        const string input = "hello world";
        var expected256 = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)));
        var expected512 = Convert.ToBase64String(SHA512.HashData(System.Text.Encoding.UTF8.GetBytes(input)));

        Assert.Equal(expected256, input.Sha256());
        Assert.Equal(expected512, input.Sha512());
        Assert.Equal(expected256, Hashing.Sha256Base64(input));
        Assert.Equal(expected512, Hashing.Sha512Base64(input));
    }

    [Fact]
    public void Sha256Base64Url_Matches_Manual_Computation()
    {
        const string input = "hello world";
        var expected = Base64Url.Encode(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)));

        Assert.Equal(expected, Hashing.Sha256Base64Url(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void String_Extensions_Return_Empty_For_Missing_Input(string? input)
    {
        Assert.Equal(string.Empty, input.Sha256());
        Assert.Equal(string.Empty, input.Sha512());
    }

    [Fact]
    public void LeftHalfHashBase64Url_Matches_Oidc_AtHash_Vector()
    {
        // OIDC Core 1.0 §A.3 example: RS256 (SHA-256) at_hash for the given access_token.
        const string accessToken = "jHkWEdUXMU1BwAsC4vtUsZwnNvTIxEl0z9K3vx5KF0Y";

        Assert.Equal("77QmUPtjPfzWtF2AnpK9RQ", Hashing.LeftHalfHashBase64Url(accessToken, HashAlgorithmName.SHA256));
    }

    [Fact]
    public void LeftHalfHashBase64Url_Sha256_Is_Left_Half_Of_Ascii_Hash() => AssertLeftHalf(HashAlgorithmName.SHA256);

    [Fact]
    public void LeftHalfHashBase64Url_Sha384_Is_Left_Half_Of_Ascii_Hash() => AssertLeftHalf(HashAlgorithmName.SHA384);

    [Fact]
    public void LeftHalfHashBase64Url_Sha512_Is_Left_Half_Of_Ascii_Hash() => AssertLeftHalf(HashAlgorithmName.SHA512);

    [Fact]
    public void LeftHalfHashBase64Url_Unsupported_Algorithm_Throws()
    {
        Assert.Throws<ArgumentException>(() => Hashing.LeftHalfHashBase64Url("x", HashAlgorithmName.MD5));
    }

    private static void AssertLeftHalf(HashAlgorithmName algorithm)
    {
        const string value = "some-authorization-code";
        var full = AsciiHash(value, algorithm);
        var expected = Base64Url.Encode(full.AsSpan(0, full.Length / 2));

        Assert.Equal(expected, Hashing.LeftHalfHashBase64Url(value, algorithm));
    }

    private static byte[] AsciiHash(string value, HashAlgorithmName algorithm)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);

        if (algorithm == HashAlgorithmName.SHA256)
            return SHA256.HashData(bytes);
        if (algorithm == HashAlgorithmName.SHA384)
            return SHA384.HashData(bytes);

        return SHA512.HashData(bytes);
    }
}
