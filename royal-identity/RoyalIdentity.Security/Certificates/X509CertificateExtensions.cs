using System.ComponentModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using RoyalIdentity.Security.Encoding;

namespace RoyalIdentity.Security.Certificates;

/// <summary>
/// Generic, environment-free extensions for <see cref="X509Certificate2"/>.
/// </summary>
/// <remarks>
/// Only the purely technical thumbprint primitive lives here (testable with an in-memory certificate).
/// The fluent certificate-store finder (<c>X509</c>) stays in the IdP core because it depends on the
/// operating system certificate store, which would make CI brittle.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class X509CertificateExtensions
{
    /// <summary>
    /// Create the value of a thumbprint-based <c>cnf</c> claim (RFC 8705 <c>x5t#S256</c>).
    /// </summary>
    public static string CreateThumbprintCnf(this X509Certificate2 certificate)
    {
        var hash = certificate.GetCertHash(HashAlgorithmName.SHA256);

        var values = new Dictionary<string, string>
        {
            { "x5t#S256", Base64Url.Encode(hash) }
        };

        return JsonSerializer.Serialize(values);
    }
}
