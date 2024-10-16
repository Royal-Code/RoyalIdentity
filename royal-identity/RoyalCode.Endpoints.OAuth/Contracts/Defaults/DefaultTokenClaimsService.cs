using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultTokenClaimsService : ITokenClaimsService
{
    private readonly ILogger<DefaultTokenClaimsService> logger;

    public Task<IEnumerable<Claim>> GetIdentityTokenClaimsAsync(
        ClaimsPrincipal subject,
        Resources resources,
        bool includeAllIdentityClaims,
        IWithClient request)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Claim>> GetAccessTokenClaimsAsync(
        ClaimsPrincipal subject,
        Resources resources,
        IWithClient context)
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

        // add scopes (filter offline_access)
        // we use the ScopeValues collection rather than the Resources.Scopes because we support dynamic scope values
        // from the context, so this issues those in the token.
        outputClaims.AddRange(resources.RequestedScopes
            .Where(x => x != ServerConstants.StandardScopes.OfflineAccess)
            .Select(scope => new Claim(JwtClaimTypes.Scope, scope)));

        if (resources.OfflineAccess)
            outputClaims.Add(new Claim(JwtClaimTypes.Scope, ServerConstants.StandardScopes.OfflineAccess));

        logger.LogDebug("Getting claims for access token for subject: {Subject}", subject.GetSubjectId());

        outputClaims.AddRange(GetStandardSubjectClaims(subject));
        outputClaims.AddRange(GetOptionalClaims(subject));

        // fetch all resource claims that need to go into the access token
        var additionalClaimTypes = resources.ApiResources.SelectMany(api => api.UserClaims).ToList();
        additionalClaimTypes.AddRange(resources.ApiScopes.SelectMany(scope => scope.UserClaims));

        // filter so we don't ask for claim types that we will eventually filter out
        additionalClaimTypes = FilterRequestedClaimTypes(additionalClaimTypes).ToList();

        var profileDataRequest = new ProfileDataRequest(
            subject,
            context.Client,
            ServerConstants.ProfileDataCallers.ClaimsProviderAccessToken,
            additionalClaimTypes.Distinct())
        {
            RequestedResources = resourceResult,
            ValidatedRequest = context
        };

        await Profile.GetProfileDataAsync(profileDataRequest);

        var claims = FilterProtocolClaims(profileDataRequest.IssuedClaims);
        if (claims != null)
        {
            outputClaims.AddRange(claims);
        }


        return outputClaims;
    }
}