using System.Security.Cryptography;
using RoyalIdentity.Security.Passwords;

namespace Tests.Security.Passwords;

public class PasswordHashTests
{
    [Fact]
    public void Create_Produces_Self_Describing_Versioned_Format()
    {
        var hash = PasswordHash.Create("pa55word");

        Assert.StartsWith("$RIPWD$1$PBKDF2-SHA256$100000$", hash);
    }

    [Fact]
    public void Verify_Returns_True_For_Correct_Password()
    {
        const string password = "correct horse battery staple";
        var hash = PasswordHash.Create(password);

        Assert.True(PasswordHash.Verify(password, hash));
    }

    [Fact]
    public void Verify_Returns_False_For_Wrong_Password()
    {
        var hash = PasswordHash.Create("right");

        Assert.False(PasswordHash.Verify("wrong", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$PBKDF2$.only-one-part")]
    [InlineData("$RIPWD$1$PBKDF2-SHA256$100000$@@@@$@@@@")]      // invalid base64 segments
    [InlineData("$RIPWD$1$PBKDF2-SHA256$notanumber$AAAA$BBBB")] // non-numeric iterations
    [InlineData("$RIPWD$99$PBKDF2-SHA256$100000$AAAA$BBBB")]    // unknown version
    [InlineData("$RIPWD$1$PBKDF2-MD5$100000$AAAA$BBBB")]        // unsupported algorithm token
    public void Verify_Returns_False_For_Malformed_Hash_Without_Throwing(string storedHash)
    {
        // Must not throw and must not authenticate.
        Assert.False(PasswordHash.Verify("whatever", storedHash));
    }

    [Fact]
    public void Verify_Returns_False_For_PreRelease_Pbkdf2_Format()
    {
        const string password = "pre-release-password";
        var preReleaseHash = CreatePreReleasePbkdf2Hash(password);

        Assert.False(PasswordHash.Verify(password, preReleaseHash));
    }

    [Fact]
    public void Verify_Returns_True_For_Hash_Created_With_Non_Default_Algorithm()
    {
        // The format is self-describing, so a hash created with a different PRF still verifies.
        var sha512 = PasswordHash.Create("x", new PasswordHashOptions { Algorithm = HashAlgorithmName.SHA512 });

        Assert.True(PasswordHash.Verify("x", sha512));
    }

    [Fact]
    public void Create_Uses_Random_Salt_So_Same_Password_Produces_Different_Hashes()
    {
        Assert.NotEqual(PasswordHash.Create("same"), PasswordHash.Create("same"));
    }

    [Fact]
    public void Create_Does_Not_Embed_Plaintext_Password()
    {
        const string password = "Sup3rSecret!Value";

        Assert.DoesNotContain(password, PasswordHash.Create(password));
    }

    // Pre-release format removed before the first final release: PBKDF2-HMAC-SHA256, 100,000 iterations,
    // 16-byte salt, 32-byte hash, formatted as "$PBKDF2$.{base64 salt}.{base64 hash}".
    private static string CreatePreReleasePbkdf2Hash(string password)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

        return $"$PBKDF2$.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
