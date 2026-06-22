using RoyalIdentity.Security.Encoding;

namespace Tests.Security.Encoding;

public class Base64UrlTests
{
    // RFC 4648 §10 test vectors; none contain '+' or '/', so base64url == base64-without-padding here.
    public static TheoryData<string, string> KnownVectors => new()
    {
        { "", "" },
        { "f", "Zg" },
        { "fo", "Zm8" },
        { "foo", "Zm9v" },
        { "foob", "Zm9vYg" },
        { "fooba", "Zm9vYmE" },
        { "foobar", "Zm9vYmFy" },
    };

    [Theory]
    [MemberData(nameof(KnownVectors))]
    public void Encode_Decode_Roundtrip_Known_Vectors(string text, string expected)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);

        var encoded = Base64Url.Encode(bytes);
        Assert.Equal(expected, encoded);

        Assert.Equal(bytes, Base64Url.Decode(encoded));
    }

    [Fact]
    public void Encode_Uses_UrlSafe_Alphabet_Without_Padding()
    {
        // 0xFB 0xFF is "+/8=" in standard base64; url-safe must render it as "-_8" (no padding).
        Assert.Equal("-_8", Base64Url.Encode(new byte[] { 0xFB, 0xFF }));
    }

    [Fact]
    public void Decode_Accepts_Input_With_And_Without_Padding()
    {
        var expected = System.Text.Encoding.UTF8.GetBytes("fo");

        Assert.Equal(expected, Base64Url.Decode("Zm8"));   // without padding
        Assert.Equal(expected, Base64Url.Decode("Zm8="));  // with padding
    }

    [Fact]
    public void Decode_Null_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Base64Url.Decode(null!));
    }

    [Fact]
    public void Decode_Invalid_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => Base64Url.Decode("not valid @@@@"));
    }

    [Fact]
    public void Decode_Invalid_Length_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => Base64Url.Decode("A"));
    }

    [Fact]
    public void TryDecode_Valid_Returns_True_With_Bytes()
    {
        Assert.True(Base64Url.TryDecode("Zm9vYmFy", out var bytes));
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("foobar"), bytes);
    }

    [Fact]
    public void TryDecode_Invalid_Returns_False_And_Empty()
    {
        Assert.False(Base64Url.TryDecode("not valid @@@@", out var bytes));
        Assert.Empty(bytes);
    }

    [Fact]
    public void TryDecode_Invalid_Length_Returns_False_And_Empty()
    {
        Assert.False(Base64Url.TryDecode("A", out var bytes));
        Assert.Empty(bytes);
    }

    [Fact]
    public void TryDecode_Null_Returns_False_And_Empty()
    {
        Assert.False(Base64Url.TryDecode(null!, out var bytes));
        Assert.Empty(bytes);
    }
}
