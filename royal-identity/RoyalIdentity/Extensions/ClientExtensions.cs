// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Extensions;

/// <summary>
/// Extension methods for client.
/// </summary>
internal static class ClientExtensions
{
    /// <summary>
    /// Constructs a list of SecurityKey from a Secret collection
    /// </summary>
    /// <param name="secrets">The secrets</param>
    /// <returns></returns>
    public static Task<List<SecurityKey>> GetKeysAsync(this IEnumerable<ClientSecret> secrets)
    {
        var secretList = secrets.ToList().AsReadOnly();
        var keys = new List<SecurityKey>();

        var certificates = GetCertificates(secretList)
            .Select(c => (SecurityKey)new X509SecurityKey(c))
            .ToList();
        keys.AddRange(certificates);

        var jwks = secretList
            .Where(s => s.Type == Server.SecretTypes.JsonWebKey)
            .Select(s => new JsonWebKey(s.Value))
            .ToList();
        keys.AddRange(jwks);

        return Task.FromResult(keys);
    }

    private static List<X509Certificate2> GetCertificates(IEnumerable<ClientSecret> secrets)
    {
        return secrets
            .Where(s => s.Type == Server.SecretTypes.X509CertificateBase64)
            .Select(s => new X509Certificate2(Convert.FromBase64String(s.Value)))
            .ToList();
    }
}
