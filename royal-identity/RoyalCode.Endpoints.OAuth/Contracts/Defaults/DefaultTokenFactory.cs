using Microsoft.Extensions.Logging;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenFactory : ITokenFactory
{
    private readonly IOptions<ServerOptions> options;
    private readonly ITokenClaimsService tokenClaimsService;
    private readonly IJwtFactory jwtFactory;
    private readonly IAccessTokenStore accessTokenStore;
    private readonly IKeyManager keys;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public DefaultTokenFactory(
        IOptions<ServerOptions> options,
        ITokenClaimsService tokenClaimsService,
        IJwtFactory jwtFactory,
        IAccessTokenStore accessTokenStore,
        IKeyManager keys,
        TimeProvider clock,
        ILogger<DefaultTokenFactory> logger)
    {
        this.options = options;
        this.tokenClaimsService = tokenClaimsService;
        this.jwtFactory = jwtFactory;
        this.accessTokenStore = accessTokenStore;
        this.keys = keys;
        this.clock = clock;
        this.logger = logger;
    }

    public async Task<AccessToken> CreateAccessTokenAsync(AccessTokenRequest request, CancellationToken ct)
    {
        logger.LogDebug("Creating access token");

        request.Context.AssertHasClient();

        var claims = new List<Claim>();
        claims.AddRange(await tokenClaimsService.GetAccessTokenClaimsAsync(
            request.Subject,
            request.Resources,
            request.Context,
            ct));

        var jti = CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex);
        if (request.Context.Client.IncludeJwtId)
        {
            claims.Add(new Claim(JwtClaimTypes.JwtId, jti));
        }

        // add session id claim
        var sid = request.Subject.GetSessionId();
        claims.Add(new Claim(JwtClaimTypes.SessionId, sid));

        // iat claim as required by JWT profile
        claims.Add(new Claim(JwtClaimTypes.IssuedAt, clock.GetUtcNow().ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));

        var issuer = request.Context.HttpContext.GetServerIssuerUri(options.Value);

        var token = new AccessToken(
            request.Context.Client.Id,
            issuer,
            AccessTokenType.Jwt,
            clock.GetUtcNow().UtcDateTime,
            request.Context.Client.AccessTokenLifetime,
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

        // add cnf if present
        if (request.Context.Confirmation.IsPresent())
        {
            token.Confirmation = request.Context.Confirmation;
        }
        else
        {
            if (options.Value.MutualTls.AlwaysEmitConfirmationClaim)
            {
                var clientCertificate = await request.Context.HttpContext.Connection.GetClientCertificateAsync();
                if (clientCertificate != null)
                {
                    token.Confirmation = clientCertificate.CreateThumbprintCnf();
                }
            }
        }

        if (token.AccessTokenType == AccessTokenType.Jwt)
        {
            logger.LogDebug("Creating JWT access token");

            await jwtFactory.CreateTokenAsync(token, ct);
        }

        await accessTokenStore.StoreAsync(token, ct);

        return token;
    }

    public async Task<IdentityToken> CreateIdentityTokenAsync(IdentityTokenRequest request, CancellationToken ct)
    {
        logger.LogDebug("Creating access token");

        request.Context.AssertHasClient();

        var credential = await keys.GetSigningCredentialsAsync(
            request.Context.Client.AllowedIdentityTokenSigningAlgorithms, 
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
        var sid = request.Subject.GetSessionId();
        claims.Add(new Claim(JwtClaimTypes.SessionId, sid));

        claims.AddRange(await tokenClaimsService.GetIdentityTokenClaimsAsync(
            request.Subject,
            request.Resources,
            request.AccessTokenToHash.IsPresent(),
            request.Context));

        var issuer = request.Context.HttpContext.GetServerIssuerUri(options.Value);

        var idToken = new IdentityToken(request.Context.Client.Id,
            issuer,
            clock.GetUtcNow().UtcDateTime,
            request.Context.Client.IdentityTokenLifetime)
        {
            AllowedSigningAlgorithms = request.Resources.ApiResources.FindMatchingSigningAlgorithms()
        };

        await jwtFactory.CreateTokenAsync(idToken, ct);

        return idToken;
    }
}
