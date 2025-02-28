using Microsoft.Extensions.Logging;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenFactory : ITokenFactory
{
    private readonly ServerOptions options;
    private readonly ITokenClaimsService tokenClaimsService;
    private readonly IJwtFactory jwtFactory;
    private readonly IAccessTokenStore accessTokenStore;
    private readonly IRefreshTokenStore refreshTokenStore;
    private readonly IKeyManager keys;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public DefaultTokenFactory(
        IOptions<ServerOptions> options,
        ITokenClaimsService tokenClaimsService,
        IJwtFactory jwtFactory,
        IAccessTokenStore accessTokenStore,
        IRefreshTokenStore refreshTokenStore,
        IKeyManager keys,
        TimeProvider clock,
        ILogger<DefaultTokenFactory> logger)
    {
        this.options = options.Value;
        this.tokenClaimsService = tokenClaimsService;
        this.jwtFactory = jwtFactory;
        this.accessTokenStore = accessTokenStore;
        this.refreshTokenStore = refreshTokenStore;
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

        var jti = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex);
        if (request.Client.IncludeJwtId)
        {
            claims.Add(new Claim(JwtClaimTypes.JwtId, jti));
        }

        // add session id claim
        if (request.IdentityType == OidcConstants.IdentityProfileTypes.User)
        {
            var sid = request.User.GetSessionId();
            claims.Add(new Claim(JwtClaimTypes.SessionId, sid));
        }

        // iat claim as required by JWT profile
        claims.Add(new Claim(JwtClaimTypes.IssuedAt, clock.GetUtcNow().ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));

        var issuer = request.HttpContext.GetServerIssuerUri(request.Client.Realm.Options);

        var token = new AccessToken(
            request.Client.Id,
            issuer,
            AccessTokenType.Jwt,
            clock.GetUtcNow().UtcDateTime,
            request.Client.AccessTokenLifetime,
            jti,
            OidcConstants.TokenResponse.BearerTokenType)
        {
            AllowedSigningAlgorithms = request.Resources.ApiResources.FindMatchingSigningAlgorithms()
        };
        token.Claims.AddRange(claims);

        // add aud based on ApiResources in the validated request
        foreach (var aud in request.Resources.ApiResources.Select(x => x.Name).Distinct())
        {
            token.Audiences.Add(aud);
        }

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
            if (options.MutualTls.AlwaysEmitConfirmationClaim)
            {
                var clientCertificate = await request.HttpContext.Connection.GetClientCertificateAsync(ct);
                if (clientCertificate is not null)
                {
                    token.Confirmation = clientCertificate.CreateThumbprintCnf();
                }
            }
        }

        if (token.AccessTokenType == AccessTokenType.Jwt)
        {
            logger.LogDebug("Creating JWT access token");

            await jwtFactory.CreateTokenAsync(request.Client.Realm, token, ct);
        }

        await accessTokenStore.StoreAsync(token, ct);

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
            claims.Add(new Claim(JwtClaimTypes.Nonce, request.Nonce));
        }

        // add iat claim
        claims.Add(new Claim(
            JwtClaimTypes.IssuedAt, 
            clock.GetUtcNow().ToUnixTimeSeconds().ToString(), 
            ClaimValueTypes.Integer64));

        // add at_hash claim
        if (request.AccessTokenToHash.IsPresent())
        {
            claims.Add(new Claim(
                JwtClaimTypes.AccessTokenHash,
                CryptoHelper.CreateHashClaimValue(request.AccessTokenToHash, signingAlgorithm)));
        }

        // add c_hash claim
        if (request.AuthorizationCodeToHash.IsPresent())
        {
            claims.Add(new Claim(
                JwtClaimTypes.AuthorizationCodeHash, 
                CryptoHelper.CreateHashClaimValue(request.AuthorizationCodeToHash, signingAlgorithm)));
        }

        // add s_hash claim
        if (request.StateHash.IsPresent())
        {
            claims.Add(new Claim(JwtClaimTypes.StateHash, request.StateHash));
        }

        // add sid
        var sid = request.User.GetSessionId();
        claims.Add(new Claim(JwtClaimTypes.SessionId, sid));

        claims.AddRange(await tokenClaimsService.GetIdentityTokenClaimsAsync(
            request.User,
            request.Resources,
            client,
            request.AccessTokenToHash.IsPresent(),
            ct));

        // add client_id to audiences if is openid
        if (request.Resources.IsOpenId)
        {
            claims.Add(new Claim(JwtClaimTypes.Audience, client.Id));
        }

        var issuer = request.HttpContext.GetServerIssuerUri(request.Client.Realm.Options);

        var idToken = new IdentityToken(client.Id,
            issuer,
            clock.GetUtcNow().UtcDateTime,
            client.IdentityTokenLifetime)
        {
            AllowedSigningAlgorithms = request.Resources.ApiResources.FindMatchingSigningAlgorithms()
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
        
        var refreshToken = new RefreshToken(
            request.Subject.GetSubjectId(),
            request.Subject.GetSessionId(),
            request.AccessToken.Id,
            request.AccessToken.Scopes.ToList(),
            client.Id,
            issuer,
            clock.GetUtcNow().UtcDateTime,
            lifetime, 
            tokenItSelf);

        await refreshTokenStore.StoreAsync(refreshToken, ct);

        return refreshToken;
    }
}
