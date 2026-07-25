// Ignore Spelling: jwt

using Microsoft.Extensions.Logging;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Security.Certificates;
using RoyalIdentity.Security.Cryptography;
using RoyalIdentity.Utils;
using System.Security.Claims;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenFactory : ITokenFactory
{
    private readonly ITokenClaimsService tokenClaimsService;
    private readonly IJwtFactory jwtFactory;
    private readonly IStorage storage;
    private readonly IKeyManager keys;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public DefaultTokenFactory(
        ITokenClaimsService tokenClaimsService,
        IJwtFactory jwtFactory,
        IStorage storage,
        IKeyManager keys,
        TimeProvider clock,
        ILogger<DefaultTokenFactory> logger)
    {
        this.tokenClaimsService = tokenClaimsService;
        this.jwtFactory = jwtFactory;
        this.storage = storage;
        this.keys = keys;
        this.clock = clock;
        this.logger = logger;
    }

    public async Task<AccessToken> CreateAccessTokenAsync(AccessTokenRequest request, CancellationToken ct)
    {
        logger.LogDebug("Creating access token");

        var claims = new List<Claim>();
        claims.AddRange(await tokenClaimsService.GetAccessTokenClaimsAsync(
            request.User,
            request.Resources,
            request.Client,
            request.IdentityType,
            ct));

        var jti = CryptoRandom.CreateUniqueId(16, OutputFormat.Hex);
        if (request.Client.IncludeJwtId)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, jti));
        }

        // add session id claim
        if (request.IdentityType == IdentityProfileTypes.User)
        {
            var sid = request.User.GetSessionId();
            claims.Add(new Claim(JwtRegisteredClaimNames.Sid, sid));
        }

        // iat claim as required by JWT profile
        claims.Add(new Claim(JwtRegisteredClaimNames.Iat, clock.GetUtcNow().ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));

        var issuer = request.HttpContext.GetServerIssuerUri(request.Client.Realm.Options);

        var token = new AccessToken(
            request.Client.Id,
            issuer,
            AccessTokenType.Jwt,
            clock.GetUtcNow().UtcDateTime,
            request.Client.AccessTokenLifetime,
            jti,
            Oidc.Token.Response.BearerTokenType)
        {
            // signing-algorithm chain (ADR-010 #a): realm orders/filters; resource servers then client
            // act only as a restrictive filter, hierarchically (never combined). Incompatibility is rejected
            // earlier by ResourcesValidator (invalid_request), so here the resolution is always compatible.
            AllowedSigningAlgorithms = request.Resources.ResolveAccessTokenSigningAlgorithms(request.Client).Algorithms
        };
        token.Claims.AddRange(claims);

        // add aud based on the resource servers of the requested scopes
        foreach (var aud in request.Resources.GetAudiences())
        {
            token.Audiences.Add(aud);
        }

        token.ResourceUris.AddRange(request.Resources.ProtectedResources.Select(resource => resource.ResourceUri));

        // add client_id to audiences if is openid
        if (request.Resources.IsOpenId)
        {
            token.Audiences.Add(request.Client.Id);
        }

        // add cnf if present
        if (request.Confirmation.IsPresent())
        {
            token.Confirmation = request.Confirmation;
        }
        else
        {
            if (request.Client.Realm.Options.MutualTls.AlwaysEmitConfirmationClaim)
            {
                var clientCertificate = await request.HttpContext.Connection.GetClientCertificateAsync(ct);
                if (clientCertificate is not null)
                {
                    token.Confirmation = clientCertificate.CreateThumbprintCnf();
                }
            }
        }

        token.RealmId = request.Client.Realm.Id;

        if (token.AccessTokenType == AccessTokenType.Jwt)
        {
            logger.LogDebug("Creating JWT access token");

            await jwtFactory.CreateTokenAsync(request.Client.Realm, token, ct);
        }

        await storage.GetAccessTokenStore(request.Client.Realm).StoreAsync(token, ct);

        return token;
    }

    public async Task<IdentityToken> CreateIdentityTokenAsync(IdentityTokenRequest request, CancellationToken ct)
    {
        logger.LogDebug("Creating access token");

        var client = request.Client;

        var credential = await keys.GetSigningCredentialsAsync(
            client.Realm,
            client.AllowedIdentityTokenSigningAlgorithms, 
            ct)
            ?? throw new InvalidOperationException("No signing credential is configured.");
            
        var signingAlgorithm = credential.Algorithm;

        // host provided claims
        var claims = new List<Claim>();

        // if nonce was sent, must be mirrored in id token
        if (request.Nonce.IsPresent())
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, request.Nonce));
        }

        // add iat claim
        claims.Add(new Claim(
            JwtRegisteredClaimNames.Iat,
            clock.GetUtcNow().ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));

        // add at_hash claim
        if (request.AccessTokenToHash.IsPresent())
        {
            claims.Add(new Claim(
                JwtRegisteredClaimNames.AtHash,
                CryptoHelper.CreateHashClaimValue(request.AccessTokenToHash, signingAlgorithm)));
        }

        // add c_hash claim
        if (request.AuthorizationCodeToHash.IsPresent())
        {
            claims.Add(new Claim(
                JwtRegisteredClaimNames.CHash,
                CryptoHelper.CreateHashClaimValue(request.AuthorizationCodeToHash, signingAlgorithm)));
        }

        // add s_hash claim
        if (request.StateHash.IsPresent())
        {
            claims.Add(new Claim(Jwt.ClaimTypes.StateHash, request.StateHash));
        }

        // add sid
        var sid = request.User.GetSessionId();
        claims.Add(new Claim(JwtRegisteredClaimNames.Sid, sid));

        // DF32: a snapshot renewal reproduces the claims of the grant, so it must not consult the provider here
        // either — otherwise the identity token and the access token of the same response would disagree about
        // the user.
        claims.AddRange(request.SnapshotClaims ?? await tokenClaimsService.GetIdentityTokenClaimsAsync(
            request.User,
            request.Resources,
            client,
            request.AccessTokenToHash.IsPresent(),
            ct));

        // add client_id to audiences if is openid
        if (request.Resources.IsOpenId)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Aud, client.Id));
        }

        var issuer = request.HttpContext.GetServerIssuerUri(request.Client.Realm.Options);

        var idToken = new IdentityToken(client.Id,
            issuer,
            clock.GetUtcNow().UtcDateTime,
            client.IdentityTokenLifetime)
        {
            // id token is signed for the client, not the resource servers (apontamento 2.5):
            // use the client's identity-token signing algorithms (realm default when empty).
            AllowedSigningAlgorithms = [.. client.AllowedIdentityTokenSigningAlgorithms],
            RealmId = request.Client.Realm.Id,
        };

        idToken.Claims.AddRange(claims);

        await jwtFactory.CreateTokenAsync(request.Client.Realm, idToken, ct);

        return idToken;
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct)
    {
        logger.LogDebug("Creating refresh token");

        Client client = request.Client;

        int lifetime;
        if (client.RefreshTokenExpiration == TokenExpiration.Absolute)
        {
            logger.LogDebug("Setting an absolute lifetime: {AbsoluteLifetime}", client.AbsoluteRefreshTokenLifetime);
            lifetime = client.AbsoluteRefreshTokenLifetime;
        }
        else
        {
            lifetime = client.SlidingRefreshTokenLifetime;
            if (client.AbsoluteRefreshTokenLifetime > 0 && lifetime > client.AbsoluteRefreshTokenLifetime)
            {
                logger.LogWarning(
                    "Client {ClientId}'s configured SlidingRefreshTokenLifetime" +
                    " of {SlidingLifetime} exceeds its AbsoluteRefreshTokenLifetime" +
                    " of {AbsoluteLifetime}. The refresh_token's sliding lifetime will be capped to the absolute lifetime",
                    client.Id, 
                    lifetime,
                    client.AbsoluteRefreshTokenLifetime);

                lifetime = client.AbsoluteRefreshTokenLifetime;
            }

            logger.LogDebug("Setting a sliding lifetime: {SlidingLifetime}", lifetime);
        }

        var issuer = request.HttpContext.GetServerIssuerUri(request.Client.Realm.Options);
        var tokenItSelf = CryptoRandom.CreateUniqueId();
        
        // DF41: the newly issued access token is an in-memory source only — the grant it carries — and no
        // identifier of it is persisted. A refresh never depends on that access token's row still existing.
        var claimsMode = request.Client.Realm.Options.RefreshTokens.ClaimsMode;

        var refreshToken = new RefreshToken(
            request.Subject.GetSubjectId(),
            request.Subject.GetSessionId(),
            request.AccessToken.Scopes.ToList(),
            client.Id,
            issuer,
            clock.GetUtcNow().UtcDateTime,
            lifetime,
            tokenItSelf)
        {
            // DF32: the mode is captured now, so a later realm change does not reinterpret this token.
            ClaimsMode = claimsMode,
        };

        refreshToken.ResourceUris.AddRange(request.AccessToken.ResourceUris);
        refreshToken.RealmId = request.Client.Realm.Id;

        // Current keeps only the protocol context needed to rebuild the principal; Snapshot additionally keeps
        // the emitted claims, so a renewal can reproduce them without consulting the claims provider (DF32).
        refreshToken.Claims.AddRange(claimsMode is RefreshTokenClaimsMode.Snapshot
            ? request.AccessToken.Claims.Where(claim => !IsReissuedPerToken(claim.Type))
            : request.AccessToken.Claims.Where(claim => ProtocolContextClaims.Contains(claim.Type)));

        if (claimsMode is RefreshTokenClaimsMode.Snapshot)
        {
            var includesIdentityToken = request.AccessToken.Scopes.Contains(Server.StandardScopes.OpenId);
            if (includesIdentityToken && request.IdentityTokenClaims is null)
            {
                throw new InvalidOperationException(
                    "Snapshot refresh tokens issued for an OpenID grant require the identity-token claims.");
            }

            refreshToken.IdentityTokenClaims.AddRange(
                request.IdentityTokenClaims?.Where(claim => !IsIdentityTokenInstanceClaim(claim.Type)) ?? []);
        }

        await storage.GetRefreshTokenStore(request.Client.Realm).StoreAsync(refreshToken, ct);

        return refreshToken;
    }

    /// <summary>
    /// The protocol context a renewal needs to rebuild the principal in <c>Current</c> mode: who authenticated,
    /// when, how and through which provider. Profile claims are deliberately absent — <c>Current</c> asks the
    /// claims provider for those again on every renewal (DF32).
    /// </summary>
    private static readonly HashSet<string> ProtocolContextClaims = new(StringComparer.Ordinal)
    {
        JwtRegisteredClaimNames.Sub,
        JwtRegisteredClaimNames.Sid,
        JwtRegisteredClaimNames.AuthTime,
        Jwt.ClaimTypes.IdentityProvider,
        JwtRegisteredClaimNames.Amr,
    };

    /// <summary>
    /// Claims that belong to one token instance and are minted again for every issuance, so keeping them in a
    /// snapshot would only let a stale value leak into a renewed token.
    /// </summary>
    private static bool IsReissuedPerToken(string claimType)
        => claimType is JwtRegisteredClaimNames.Jti
            or JwtRegisteredClaimNames.Iat
            or JwtRegisteredClaimNames.Exp
            or JwtRegisteredClaimNames.Nbf;

    /// <summary>
    /// Claims tied to one identity-token instance. Snapshot mode persists only subject/profile claims; these
    /// values are recalculated for every renewed identity token.
    /// </summary>
    private static bool IsIdentityTokenInstanceClaim(string claimType)
        => claimType is JwtRegisteredClaimNames.Sid
            or JwtRegisteredClaimNames.Jti
            or JwtRegisteredClaimNames.Iat
            or JwtRegisteredClaimNames.Exp
            or JwtRegisteredClaimNames.Nbf
            or JwtRegisteredClaimNames.Aud
            or JwtRegisteredClaimNames.Iss
            or JwtRegisteredClaimNames.AtHash
            or JwtRegisteredClaimNames.CHash
            or JwtRegisteredClaimNames.Nonce
            or Jwt.ClaimTypes.StateHash;
}
