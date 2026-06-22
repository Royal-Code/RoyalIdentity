using RoyalIdentity.Security.Certificates;
using RoyalIdentity.Security.Encoding;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Tests.Security.Certificates;

public class X509CertificateExtensionsTests
{
    private static X509Certificate2 CreateInMemoryCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=royalidentity-test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1));
    }

    [Fact]
    public void CreateThumbprintCnf_Produces_Base64Url_Sha256_Thumbprint()
    {
        using var certificate = CreateInMemoryCertificate();

        var cnf = certificate.CreateThumbprintCnf();

        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(cnf);
        Assert.NotNull(parsed);
        Assert.True(parsed!.ContainsKey("x5t#S256"));

        var expected = Base64Url.Encode(certificate.GetCertHash(HashAlgorithmName.SHA256));
        Assert.Equal(expected, parsed["x5t#S256"]);

        // Base64Url: no padding and URL-safe alphabet.
        Assert.DoesNotContain('=', parsed["x5t#S256"]);
        Assert.DoesNotContain('+', parsed["x5t#S256"]);
        Assert.DoesNotContain('/', parsed["x5t#S256"]);
    }
}
