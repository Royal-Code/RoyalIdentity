using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenFactory : ITokenFactory
{
    private readonly ILogger logger;
    private readonly ITokenClaimsService tokenClaimsService;
    private readonly TimeProvider clock;
    private readonly IOptions<ServerOptions> options;

    public async Task<AccessToken> CreateAccessTokenAsync(AccessTokenRequest request, CancellationToken ct)
    {
        logger.LogTrace("Creating access token");

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

        if (options.Value.EmitStaticAudienceClaim)
        {
            token.Audiences.Add(string.Format(ServerConstants.AccessTokenAudience, [issuer.EnsureTrailingSlash()]));
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

        await CreateAccessTokenAsync(token, ct);

        return token;
    }

    public Task<Token> CreateIdentityTokenAsync(IdentityTokenRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a serialized and protected security token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>
    /// A security token in serialized form
    /// </returns>
    /// <exception cref="System.InvalidOperationException">Invalid token type.</exception>
    public virtual async Task<string> CreateAccessTokenAsync(AccessToken token, CancellationToken ct)
    {
        string tokenResult;


        if (token.AccessTokenType == AccessTokenType.Jwt)
        {
            logger.LogTrace("Creating JWT access token");

            tokenResult = await CreationService.CreateTokenAsync(token, ct);
        }
        else
        {
            logger.LogTrace("Creating reference access token");

            var handle = await ReferenceTokenStore.StoreReferenceTokenAsync(token);

            tokenResult = handle;
        }



        // else if (token.Type == OidcConstants.TokenTypes.IdentityToken)
        // {
        //     Logger.LogTrace("Creating JWT identity token");
        //
        //     tokenResult = await CreationService.CreateTokenAsync(token);
        // }
        // else
        // {
        //     throw new InvalidOperationException("Invalid token type.");
        // }

        return tokenResult;
    }
}
