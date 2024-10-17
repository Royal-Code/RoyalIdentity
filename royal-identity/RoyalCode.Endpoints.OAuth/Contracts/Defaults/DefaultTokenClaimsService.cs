using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
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

    public Task<IEnumerable<Claim>> GetIdentityTokenClaimsAsync(
        ClaimsPrincipal subject,
        Resources resources,
        bool includeAllIdentityClaims,
        IWithClient context)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Claim>> GetAccessTokenClaimsAsync(
        ClaimsPrincipal subject,
        Resources resources,
        IWithClient context,
        CancellationToken ct)
    {
        context.AssertHasClient();

        logger.LogDebug("Getting claims for access token for client: {ClientId}", context.Client.Id);

        var outputClaims = new List<Claim>
        {
            new(JwtClaimTypes.ClientId, context.ClientId)
        };

        // log if client ID is overwritten
        if (!string.Equals(context.ClientId, context.Client.Id))
        {
            logger.LogDebug("Client {ClientId} is impersonating {ImpersonatedClientId}",
                context.Client.Id,
                context.ClientId);
        }

        // check for client claims
        if (context.Client.Claims.Count is not 0 && context.Client.AlwaysSendClientClaims)
        {
            foreach (var claim in context.ClientClaims)
            {
                var claimType = claim.Type;

                if (context.Client.ClientClaimsPrefix.IsPresent())
                {
                    claimType = context.Client.ClientClaimsPrefix + claimType;
                }

                outputClaims.Add(new Claim(claimType, claim.Value, claim.ValueType));
            }
        }

        // add scopes
        outputClaims.AddRange(resources.RequestedScopes.Select(scope => new Claim(JwtClaimTypes.Scope, scope)));

        logger.LogDebug("Getting claims for access token for subject: {Subject}", subject.GetSubjectId());

        outputClaims.AddRange(GetStandardSubjectClaims(subject));
        outputClaims.AddRange(GetOptionalClaims(subject));

        // fetch all resource claims that need to go into the access token
        var additionalClaimTypes = resources.ApiResources.SelectMany(api => api.UserClaims).ToList();
        additionalClaimTypes.AddRange(resources.ApiScopes.SelectMany(scope => scope.UserClaims));

        // filter so we don't ask for claim types that we will eventually filter out
        additionalClaimTypes = FilterRequestedClaimTypes(additionalClaimTypes).ToList();

        var profileDataRequest = new ProfileDataRequest(
            context,
            resources,
            subject,
            context.Client,
            ServerConstants.ProfileDataCallers.ClaimsProviderAccessToken,
            additionalClaimTypes.Distinct());

        await profileService.GetProfileDataAsync(profileDataRequest);

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
            new (JwtClaimTypes.Subject, subject.GetSubjectId()),
            new (JwtClaimTypes.AuthenticationTime, subject.GetAuthenticationTimeEpoch().ToString(), ClaimValueTypes.Integer64),
            new (JwtClaimTypes.IdentityProvider, subject.GetIdentityProvider())
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
    protected virtual IEnumerable<Claim> FilterClaims(List<Claim> claims)
    {
        var claimsToFilter = claims
            .Where(x => Constants.Filters.ClaimsServiceFilterClaimTypes.Contains(x.Type))
            .ToList();

        if (claimsToFilter.Count is not 0)
            logger.LogDebug("Claim types that were filtered: {ClaimTypes}", claimsToFilter.Select(x => x.Type));

        return claims.Except(claimsToFilter);
    }

    /// <summary>
    /// Filters out protocol claims like amr, nonce etc..
    /// </summary>
    /// <param name="claimTypes">The claim types.</param>
    protected virtual IEnumerable<string> FilterRequestedClaimTypes(List<string> claimTypes)
    {
        var claimTypesToFilter = claimTypes.Where(x => Constants.Filters.ClaimsServiceFilterClaimTypes.Contains(x));
        return claimTypes.Except(claimTypesToFilter);
    }
}