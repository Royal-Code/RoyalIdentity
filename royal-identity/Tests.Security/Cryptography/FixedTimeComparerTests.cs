using RoyalIdentity.Security.Cryptography;

namespace Tests.Security.Cryptography;

public class FixedTimeComparerTests
{
    [Fact]
    public void IsEqual_True_For_Identical_Bytes()
    {
        Assert.True(FixedTimeComparer.IsEqual(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void IsEqual_False_For_Different_Bytes_Of_Same_Length()
    {
        Assert.False(FixedTimeComparer.IsEqual(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3, 5 }));
    }

    [Fact]
    public void IsEqual_False_For_Different_Lengths()
    {
        // FixedTimeEquals returns false immediately when lengths differ; the length is not secret here.
        Assert.False(FixedTimeComparer.IsEqual(new byte[] { 1, 2, 3, 4 }, new byte[] { 1, 2, 3 }));
    }

    [Theory]
    [InlineData("secret-value", "secret-value", true)]
    [InlineData("secret-value", "secret-walue", false)]
    [InlineData("short", "longer-string", false)]
    public void IsEqualUtf8_Compares_Utf8_Bytes(string left, string right, bool expected)
    {
        Assert.Equal(expected, FixedTimeComparer.IsEqualUtf8(left, right));
    }

    [Fact]
    public void IsEqualBase64_True_For_Same_Decoded_Bytes()
    {
        var value = Convert.ToBase64String(new byte[] { 9, 8, 7, 6, 5 });

        Assert.True(FixedTimeComparer.IsEqualBase64(value, value));
    }

    [Fact]
    public void IsEqualBase64_False_For_Different_Decoded_Bytes()
    {
        var left = Convert.ToBase64String(new byte[] { 9, 8, 7, 6, 5 });
        var right = Convert.ToBase64String(new byte[] { 9, 8, 7, 6, 4 });

        Assert.False(FixedTimeComparer.IsEqualBase64(left, right));
    }
}
