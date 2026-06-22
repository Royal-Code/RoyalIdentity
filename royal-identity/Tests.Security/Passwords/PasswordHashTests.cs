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
    public void Verify_Returns_Success_For_Correct_Password_On_Current_Hash()
    {
        const string password = "correct horse battery staple";
        var hash = PasswordHash.Create(password);

        Assert.Equal(PasswordVerificationResult.Success, PasswordHash.Verify(password, hash));
    }

    [Fact]
    public void Verify_Returns_Failed_For_Wrong_Password()
    {
        var hash = PasswordHash.Create("right");

        Assert.Equal(PasswordVerificationResult.Failed, PasswordHash.Verify("wrong", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$PBKDF2$.only-one-part")]
    [InlineData("$RIPWD$1$PBKDF2-SHA256$100000$@@@@$@@@@")]      // invalid base64 segments
    [InlineData("$RIPWD$1$PBKDF2-SHA256$notanumber$AAAA$BBBB")] // non-numeric iterations
    [InlineData("$RIPWD$99$PBKDF2-SHA256$100000$AAAA$BBBB")]    // unknown version
    [InlineData("$RIPWD$1$PBKDF2-MD5$100000$AAAA$BBBB")]        // unsupported algorithm token
    public void Verify_Returns_Failed_For_Malformed_Hash_Without_Throwing(string storedHash)
    {
        // Must not throw and must not authenticate.
        Assert.Equal(PasswordVerificationResult.Failed, PasswordHash.Verify("whatever", storedHash));
    }

    [Fact]
    public void Verify_Accepts_Legacy_Pbkdf2_Format_As_RehashNeeded()
    {
        const string password = "legacy-password";
        var legacy = CreateLegacyHash(password);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, PasswordHash.Verify(password, legacy));
        Assert.Equal(PasswordVerificationResult.Failed, PasswordHash.Verify("nope", legacy));
    }

    [Fact]
    public void NeedsRehash_True_For_Legacy_Hash()
    {
        Assert.True(PasswordHash.NeedsRehash(CreateLegacyHash("x"), PasswordHashOptions.Default));
    }

    [Fact]
    public void NeedsRehash_False_For_Current_Default_Hash()
    {
        Assert.False(PasswordHash.NeedsRehash(PasswordHash.Create("x"), PasswordHashOptions.Default));
    }

    [Fact]
    public void NeedsRehash_True_For_Fewer_Iterations()
    {
        var weaker = PasswordHash.Create("x", new PasswordHashOptions { Iterations = 50_000 });

        Assert.True(PasswordHash.NeedsRehash(weaker, PasswordHashOptions.Default)); // default is 100,000
    }

    [Fact]
    public void NeedsRehash_True_For_Different_Algorithm_And_Hash_Still_Verifies()
    {
        var sha512 = PasswordHash.Create("x", new PasswordHashOptions { Algorithm = HashAlgorithmName.SHA512 });

        Assert.True(PasswordHash.NeedsRehash(sha512, PasswordHashOptions.Default)); // default is SHA-256
        Assert.Equal(PasswordVerificationResult.Success, PasswordHash.Verify("x", sha512));
    }

    [Fact]
    public void NeedsRehash_True_For_Malformed_Hash()
    {
        Assert.True(PasswordHash.NeedsRehash("garbage", PasswordHashOptions.Default));
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

    // Mirrors RoyalIdentity/Utils/PasswordHash.cs: PBKDF2-HMAC-SHA256, 100,000 iterations, 16-byte salt,
    // 32-byte hash, formatted as "$PBKDF2$.{base64 salt}.{base64 hash}".
    private static string CreateLegacyHash(string password)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

        return $"$PBKDF2$.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
