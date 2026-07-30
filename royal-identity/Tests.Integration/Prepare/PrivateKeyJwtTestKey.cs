using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Models;

namespace Tests.Integration.Prepare;

/// <summary>
/// Asymmetric key pair for <c>private_key_jwt</c> scenarios: it produces the public JWK a client is registered
/// with and signs the client assertions the tests present. Reused by every phase of plan-replay-protection, which
/// is why it lives here and not in one test class.
/// </summary>
public sealed class PrivateKeyJwtTestKey : IDisposable
{
    private readonly RSA rsa = RSA.Create(2048);
    private bool disposed;

    public PrivateKeyJwtTestKey(string keyId = "private-key-jwt-test-key")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        KeyId = keyId;

        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        PublicJwkJson = JsonSerializer.Serialize(new
        {
            kty = "RSA",
            use = "sig",
            alg = SecurityAlgorithms.RsaSha256,
            kid = keyId,
            n = Base64UrlEncoder.Encode(parameters.Modulus),
            e = Base64UrlEncoder.Encode(parameters.Exponent),
        });
    }

    public string KeyId { get; }

    /// <summary>The public half, in the JWK form a client secret carries.</summary>
    public string PublicJwkJson { get; }

    /// <summary>The client secret that registers this key on a client.</summary>
    public ClientSecret CreateClientSecret()
        => new(PublicJwkJson, "private_key_jwt test key")
        {
            Type = RoyalIdentity.Options.Constants.Server.SecretTypes.JsonWebKey,
        };

    /// <summary>
    /// Signs a client assertion. <paramref name="expires"/> is expressed against the <b>server's</b> clock,
    /// which is what the realm ceiling is compared to: an assertion of five minutes emitted by a client whose
    /// clock runs five minutes ahead arrives here as <c>now + 10min</c>.
    /// </summary>
    public string CreateAssertion(
        string clientId,
        string audience,
        string jti,
        DateTimeOffset issuedAt,
        DateTimeOffset expires)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        var credentials = new SigningCredentials(
            new RsaSecurityKey(rsa) { KeyId = KeyId },
            SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: clientId,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, clientId),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
            ],
            notBefore: issuedAt.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        rsa.Dispose();
    }
}
