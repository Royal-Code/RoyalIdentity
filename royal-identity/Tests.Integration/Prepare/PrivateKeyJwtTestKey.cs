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
    /// Signs a client assertion. <paramref name="notBefore"/> and <paramref name="expires"/> are expressed against
    /// the <b>server's</b> clock. The former represents the client's issuance instant as the token's
    /// <c>nbf</c>; the helper deliberately does not add the optional <c>iat</c> claim. An assertion of five minutes
    /// emitted by a client whose clock runs five minutes ahead therefore arrives with
    /// <c>nbf = now + 5min</c> and <c>exp = now + 10min</c>.
    /// </summary>
    public string CreateAssertion(
        string clientId,
        string audience,
        string jti,
        DateTimeOffset notBefore,
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
            notBefore: notBefore.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Signs an assertion whose <c>nbf</c> and <c>exp</c> are written as raw claims, so the pair may be
    /// incoherent. <see cref="JwtSecurityToken"/>'s own constructor refuses <c>nbf &gt;= exp</c>, which is why a
    /// token describing no valid window has to be assembled from header and payload to be tested at all.
    /// </summary>
    public string CreateAssertionWithRawLifetime(
        string clientId,
        string audience,
        string jti,
        DateTimeOffset notBefore,
        DateTimeOffset expires)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        var credentials = new SigningCredentials(
            new RsaSecurityKey(rsa) { KeyId = KeyId },
            SecurityAlgorithms.RsaSha256);

        var payload = new JwtPayload(
        [
            new Claim(JwtRegisteredClaimNames.Iss, clientId),
            new Claim(JwtRegisteredClaimNames.Sub, clientId),
            new Claim(JwtRegisteredClaimNames.Aud, audience),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(
                JwtRegisteredClaimNames.Nbf,
                notBefore.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim(
                JwtRegisteredClaimNames.Exp,
                expires.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        ]);

        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(new JwtHeader(credentials), payload));
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        rsa.Dispose();
    }
}
