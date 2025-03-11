using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenClaimsService : ITokenClaimsService
{
    private readonly ILogger logger;
    private readonly IProfileService profileService;

    public DefaultTokenClaimsService(
        ILogger<DefaultTokenClaimsService> logger,
        IProfileService profileService)
    {
        this.logger = logger;
        this.profileService = profileService;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Claim>> GetIdentityTokenClaimsAsync(
        ClaimsPrincipal subject,
        Resources resources,
        Client client,
        bool includeAllIdentityClaims,
        CancellationToken ct)
    {
        logger.LogDebug("Getting claims for identity token for subject: {Subject} and client: {ClientId}",
            subject.GetSubjectId(),
            client.Id);

        var outputClaims = new List<Claim>(GetStandardSubjectClaims(subject));
        outputClaims.AddRange(GetOptionalClaims(subject));

        // fetch all identity claims that need to go into the id token
        if (includeAllIdentityClaims || client.AlwaysIncludeUserClaimsInIdToken)
        {
            var profileDataRequest = new ProfileDataRequest(
                resources,
                subject,
                client,
                IdentityProfileTypes.User,
                resources.RequestedIdentityClaimTypes());

            await profileService.GetProfileDataAsync(profileDataRequest, ct);

            var claims = FilterProtocolClaims(profileDataRequest.IssuedClaims);

            outputClaims.AddRange(claims);
        }
        else
        {
            logger.LogDebug(
                "In addition to an id_token, an access_token was requested. " +
                "No claims other than sub are included in the id_token. " +
                "To obtain more user claims, either use the user info endpoint or set AlwaysIncludeUserClaimsInIdToken on the client configuration.");
        }

        return outputClaims;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Claim>> GetAccessTokenClaimsAsync(
        ClaimsPrincipal subject,
        Resources resources,
        Client client,
        string identityType,
        CancellationToken ct)
    {
        logger.LogDebug("Getting claims for access token for client: {ClientId}", client.Id);

        var outputClaims = new List<Claim>
        {
            new(JwtClaimTypes.ClientId, client.Id)
        };

        // check for client claims
        if (client.Claims.Count is not 0 && client.AlwaysSendClientClaims)
        {
            foreach (var claim in client.Claims)
            {
                var claimType = claim.Type;

                if (client.ClientClaimsPrefix.IsPresent())
                {
                    claimType = client.ClientClaimsPrefix + claimType;
                }

                outputClaims.Add(new Claim(claimType, claim.Value, claim.ValueType));
            }
        }

        // add scopes
        outputClaims.AddRange(resources.RequestedScopes.Select(scope => new Claim(JwtClaimTypes.Scope, scope)));

        logger.LogDebug("Getting claims for access token for subject: {Subject}", subject.GetSubjectId());

        outputClaims.AddRange(GetStandardSubjectClaims(subject));
        outputClaims.AddRange(GetOptionalClaims(subject));

        var profileDataRequest = new ProfileDataRequest(
            resources,
            subject,
            client,
            identityType,
            resources.RequestedResourcesClaimTypes());

        await profileService.GetProfileDataAsync(profileDataRequest, ct);

        outputClaims.AddRange(FilterClaims(profileDataRequest.IssuedClaims));

        return outputClaims;
    }

    /// <summary>
    /// Gets the standard subject claims.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <returns>A list of standard claims</returns>
    protected virtual IEnumerable<Claim> GetStandardSubjectClaims(ClaimsPrincipal subject)
    {
        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, subject.GetSubjectId()),
            new(JwtClaimTypes.AuthenticationTime, subject.GetAuthenticationTimeEpoch().ToString(),
                ClaimValueTypes.Integer64),
            new(JwtClaimTypes.IdentityProvider, subject.GetIdentityProvider())
        };

        claims.AddRange(subject.GetAuthenticationMethods());

        return claims;
    }

    /// <summary>
    /// Gets additional (and optional) claims from the cookie or incoming subject.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <returns>Additional claims</returns>
    protected virtual IEnumerable<Claim> GetOptionalClaims(ClaimsPrincipal subject)
    {
        var claims = new List<Claim>();

        var acr = subject.FindFirst(JwtClaimTypes.AuthenticationContextClassReference);
        if (acr is not null)
            claims.Add(acr);

        return claims;
    }

    /// <summary>
    /// Filters out protocol claims like amr, nonce etc..
    /// </summary>
    /// <param name="claims">The claims.</param>
    /// <returns></returns>
    protected virtual IEnumerable<Claim> FilterClaims(HashSet<Claim> claims)
    {
        var claimsToFilter = claims
            .Where(x => Filters.ClaimsServiceFilterClaimTypes.Contains(x.Type))
            .ToList();

        if (claimsToFilter.Count is not 0)
            logger.LogDebug("Claim types that were filtered: {ClaimTypes}", claimsToFilter.Select(x => x.Type));

        return claims.Except(claimsToFilter);
    }

    /// <summary>
    /// Filters out protocol claims like amr, nonce etc..
    /// </summary>
    /// <param name="claims">The claims.</param>
    /// <returns></returns>
    protected virtual IEnumerable<Claim> FilterProtocolClaims(HashSet<Claim> claims)
    {
        var claimsToFilter = claims
            .Where(x => Filters.ClaimsServiceFilterClaimTypes.Contains(x.Type))
            .ToList();

        if (claimsToFilter.Count is not 0 && logger.IsEnabled(LogLevel.Debug))
        {
            var types = claimsToFilter.Select(x => x.Type);
            logger.LogDebug("Claim types from profile service that were filtered: {ClaimTypes}", types);
        }

        return claims.Except(claimsToFilter);
    }
}