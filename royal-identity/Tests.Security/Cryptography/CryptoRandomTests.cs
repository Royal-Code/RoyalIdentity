using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.Security.Encoding;

namespace Tests.Security.Cryptography;

public class CryptoRandomTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(33)]
    public void CreateRandomKey_Produces_Requested_Byte_Length(int length)
    {
        Assert.Equal(length, CryptoRandom.CreateRandomKey(length).Length);
    }

    [Fact]
    public void CreateUniqueId_Base64Url_Has_No_Padding_And_Roundtrips_To_EntropyBytes()
    {
        var id = CryptoRandom.CreateUniqueId(32, OutputFormat.Base64Url);

        Assert.DoesNotContain('=', id);
        Assert.DoesNotContain('+', id);
        Assert.DoesNotContain('/', id);
        Assert.Equal(32, Base64Url.Decode(id).Length);
    }

    [Fact]
    public void CreateUniqueId_Base64_Roundtrips_To_EntropyBytes()
    {
        var id = CryptoRandom.CreateUniqueId(32, OutputFormat.Base64);

        Assert.Equal(32, Convert.FromBase64String(id).Length);
    }

    [Fact]
    public void CreateUniqueId_Hex_Is_Uppercase_And_Roundtrips_To_EntropyBytes()
    {
        var id = CryptoRandom.CreateUniqueId(16, OutputFormat.Hex);

        Assert.Equal(32, id.Length); // 16 entropy bytes -> 32 hex chars
        Assert.Equal(id.ToUpperInvariant(), id);
        Assert.All(id, c => Assert.Contains(c, "0123456789ABCDEF"));
        Assert.Equal(16, Convert.FromHexString(id).Length);
    }

    [Fact]
    public void Next_Is_NonNegative()
    {
        for (var i = 0; i < 1000; i++)
            Assert.True(CryptoRandom.Next() >= 0);
    }

    [Fact]
    public void Next_MaxValue_Stays_In_Range()
    {
        for (var i = 0; i < 1000; i++)
            Assert.InRange(CryptoRandom.Next(10), 0, 9);

        Assert.Equal(0, CryptoRandom.Next(0));
    }

    [Fact]
    public void Next_MinMax_Stays_In_Range()
    {
        for (var i = 0; i < 1000; i++)
            Assert.InRange(CryptoRandom.Next(5, 10), 5, 9);

        Assert.Equal(7, CryptoRandom.Next(7, 7));
    }

    [Fact]
    public void NextDouble_Is_In_ZeroToOne()
    {
        for (var i = 0; i < 1000; i++)
        {
            var value = CryptoRandom.NextDouble();
            Assert.True(value is >= 0.0 and < 1.0, $"value out of range: {value}");
        }
    }

    [Fact]
    public void Next_Negative_MaxValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CryptoRandom.Next(-1));
    }

    [Fact]
    public void Next_Min_Greater_Than_Max_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CryptoRandom.Next(10, 5));
    }

    [Fact]
    public void CreateUniqueId_Generates_Distinct_Values()
    {
        const int count = 1000;
        var ids = new HashSet<string>(count);

        for (var i = 0; i < count; i++)
            Assert.True(ids.Add(CryptoRandom.CreateUniqueId()), "CreateUniqueId produced a duplicate value");

        Assert.Equal(count, ids.Count);
    }
}
