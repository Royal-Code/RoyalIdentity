using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenFactory : ITokenFactory
{
    private readonly ILogger logger;

    public async Task<AccessToken> CreateAccessTokenAsync(AccessTokenRequest request)
    {
        logger.LogTrace("Creating access token");

        var claims = new List<Claim>();
        claims.AddRange(await ClaimsProvider.GetAccessTokenClaimsAsync(
            request.Subject,
            request.ValidatedResources,
            request.ValidatedRequest));

        if (request.ValidatedRequest.Client.IncludeJwtId)
        {
            claims.Add(new Claim(JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex)));
        }

        if (request.ValidatedRequest.SessionId.IsPresent())
        {
            claims.Add(new Claim(JwtClaimTypes.SessionId, request.ValidatedRequest.SessionId));
        }

        // iat claim as required by JWT profile
        claims.Add(new Claim(JwtClaimTypes.IssuedAt, Clock.UtcNow.ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));

        var issuer = request.HttpContext.GetServerIssuerUri();

        var token = new AccessToken(OidcConstants.TokenTypes.AccessToken)
        {
            CreationTime = Clock.UtcNow.UtcDateTime,
            Issuer = issuer,
            Lifetime = request.ValidatedRequest.AccessTokenLifetime,
            Claims = claims.Distinct(new ClaimComparer()).ToList(),
            ClientId = request.ValidatedRequest.Client.ClientId,
            Description = request.Description,
            AccessTokenType = request.ValidatedRequest.AccessTokenType,
            AllowedSigningAlgorithms = request.ValidatedResources.Resources.ApiResources.FindMatchingSigningAlgorithms()
        };

        // add aud based on ApiResources in the validated request
        foreach (var aud in request.ValidatedResources.Resources.ApiResources.Select(x => x.Name).Distinct())
        {
            token.Audiences.Add(aud);
        }

        if (Options.EmitStaticAudienceClaim)
        {
            token.Audiences.Add(string.Format(IdentityServerConstants.AccessTokenAudience, issuer.EnsureTrailingSlash()));
        }

        // add cnf if present
        if (request.ValidatedRequest.Confirmation.IsPresent())
        {
            token.Confirmation = request.ValidatedRequest.Confirmation;
        }
        else
        {
            if (Options.MutualTls.AlwaysEmitConfirmationClaim)
            {
                var clientCertificate = await ContextAccessor.HttpContext.Connection.GetClientCertificateAsync();
                if (clientCertificate != null)
                {
                    token.Confirmation = clientCertificate.CreateThumbprintCnf();
                }
            }
        }

        return token;
    }

    public Task<Token> CreateIdentityTokenAsync(IdentityTokenRequest request)
    {
        throw new NotImplementedException();
    }
}
