using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Nodes;

namespace RoyalIdentity.Contracts.Defaults;

#pragma warning disable S2139 // bug sonar with exception

public class DefaultJwtFactory : IJwtFactory
{
    private readonly ServerOptions options;
    private readonly IKeyManager keys;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public DefaultJwtFactory(
        IStorage storage, 
        IKeyManager keys,
        TimeProvider clock,
        ILogger<DefaultJwtFactory> logger)
    {
        options = storage.ServerOptions;
        this.keys = keys;
        this.clock = clock;
        this.logger = logger;
    }

    public async Task CreateTokenAsync(Realm realm, TokenBase token, CancellationToken ct)
    {
        var header = await CreateHeaderAsync(realm, token, ct);
        var payload = await CreatePayloadAsync(token, ct);
        var jst = new JwtSecurityToken(header, payload);

        var jwt = await CreateJwtAsync(jst, ct);

        token.Token = jwt;
    }

    /// <summary>
    /// Creates the JWT header
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The JWT header</returns>
    protected virtual async Task<JwtHeader> CreateHeaderAsync(Realm realm, TokenBase token, CancellationToken ct)
    {
        var credential = await keys.GetSigningCredentialsAsync(realm, token.AllowedSigningAlgorithms, ct)
            ?? throw new InvalidOperationException("No signing credential is configured. Can't create JWT token");

        var header = new JwtHeader(credential)
        {
            [JwtClaimTypes.TokenType] = options.AccessTokenJwtType
        };

        // emit x5t claim for backwards compatibility with v4 of MS JWT library
        if (credential.Key is X509SecurityKey x509Key)
        {
            var cert = x509Key.Certificate;
            if (clock.GetUtcNow().UtcDateTime > cert.NotAfter)
            {
                logger.LogWarning(
                    "Certificate {SubjectName} has expired on {Expiration}", 
                    cert.Subject,
                    cert.NotAfter.ToString(CultureInfo.InvariantCulture));
            }

            header["x5t"] = Base64Url.Encode(cert.GetCertHash());
        }

        return header;
    }

    /// <summary>
    /// Creates the JWT payload
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The JWT payload</returns>
    protected virtual ValueTask<JwtPayload> CreatePayloadAsync(TokenBase token, CancellationToken ct)
    {
        var payload = CreateJwtPayload(token);
        return ValueTask.FromResult(payload);
    }

    /// <summary>
    /// Applies the signature to the JWT
    /// </summary>
    /// <param name="jwt">The JWT object.</param>
    /// <returns>The signed JWT</returns>
    protected virtual ValueTask<string> CreateJwtAsync(JwtSecurityToken jwt, CancellationToken ct)
    {
        var handler = new JwtSecurityTokenHandler();
        return ValueTask.FromResult(handler.WriteToken(jwt));
    }

    /// <summary>
    /// Creates the default JWT payload.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The options</param>
    /// <param name="logger">The logger.</param>
    /// <returns></returns>
    /// <exception cref="Exception">
    /// </exception>
    protected virtual JwtPayload CreateJwtPayload(TokenBase token)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        var payload = new JwtPayload(
            token.Issuer,
            null,
            null,
            now,
            now.AddSeconds(token.Lifetime));

        foreach (var aud in token.Audiences)
        {
            payload.AddClaim(new Claim(JwtClaimTypes.Audience, aud));
        }

        var scopeClaims = token.Claims.Where(x => x.Type == JwtClaimTypes.Scope).ToArray();
        var jsonClaims = token.Claims.Where(x => x.ValueType == ServerConstants.ClaimValueTypes.Json).ToList();

        // add confirmation claim if present (it's JSON valued)
        if (token.Confirmation.IsPresent())
        {
            jsonClaims.Add(new Claim(JwtClaimTypes.Confirmation, token.Confirmation, ServerConstants.ClaimValueTypes.Json));
        }

        var normalClaims = token.Claims
            .Except(jsonClaims)
            .Except(scopeClaims);

        payload.AddClaims(normalClaims);

        // scope claims
        if (!scopeClaims.IsNullOrEmpty())
        {
            var scopeValues = scopeClaims.Select(x => x.Value).ToArray();

            if (options.EmitScopesAsSpaceDelimitedStringInJwt)
            {
                payload.Add(JwtClaimTypes.Scope, string.Join(" ", scopeValues));
            }
            else
            {
                payload.Add(JwtClaimTypes.Scope, scopeValues);
            }
        }

        // deal with json types
        // calling ToArray() to trigger JSON parsing once and so later 
        // collection identity comparisons work for the anonymous type
        try
        {
            
            var jsonTokens = jsonClaims.Select(x => new { x.Type, JsonValue = JsonNode.Parse(x.Value) }).ToArray();
            var jsonObjects = jsonTokens.Where(x => x.JsonValue is JsonObject).ToArray();
            var jsonObjectGroups = jsonObjects.GroupBy(x => x.Type).ToArray();

            foreach (var group in jsonObjectGroups)
            {
                if (payload.ContainsKey(group.Key))
                {
                    throw new InvalidOperationException(
                        $"Can't add two claims where one is a JSON object and the other is not a JSON object ({group.Key})");
                }

                if (group.Skip(1).Any())
                {
                    // add as array
                    payload.Add(group.Key, group.Select(x => x.JsonValue).ToArray());
                }
                else
                {
                    // add just one
                    payload.Add(group.Key, group.First().JsonValue);
                }
            }

            var jsonArrays = jsonTokens.Where(x => x.JsonValue is JsonArray).ToArray();
            var jsonArrayGroups = jsonArrays.GroupBy(x => x.Type).ToArray();
            foreach (var group in jsonArrayGroups)
            {
                if (payload.ContainsKey(group.Key))
                {
                    throw new InvalidOperationException(
                        $"Can't add two claims where one is a JSON array and the other is not a JSON array ({group.Key})");
                }

                var newArr = new List<JsonNode>();
                foreach (var arrays in group)
                {
                    var arr = (JsonArray)arrays.JsonValue!;
                    newArr.AddRange(arr!);
                }

                // add just one array for the group/key/claim type
                payload.Add(group.Key, newArr.ToArray());
            }

            var unsupportedJsonTokens = jsonTokens.Except(jsonObjects).Except(jsonArrays).ToArray();
            var unsupportedJsonClaimTypes = unsupportedJsonTokens.Select(x => x.Type).Distinct().ToArray();
            if (unsupportedJsonClaimTypes.Length is not 0)
            {
                throw new InvalidOperationException(
                    $"Unsupported JSON type for claim types: {unsupportedJsonClaimTypes.Aggregate((x, y) => x + ", " + y)}");
            }

            return payload;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating a JSON valued claim");
            throw;
        }
    }
}
