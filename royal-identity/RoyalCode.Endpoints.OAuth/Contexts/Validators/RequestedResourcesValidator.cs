using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class RequestedResourcesValidator : IValidator<AuthorizeContext>
{
    private readonly ServerOptions options;
    private readonly IResourceStore resourceStore;
    private readonly ILogger logger;

    public RequestedResourcesValidator(
        IOptions<ServerOptions> options,
        IResourceStore resourceStore,
        ILogger<RequestedResourcesValidator> logger)
    {
        this.options = options.Value;
        this.resourceStore = resourceStore;
        this.logger = logger;
    }

    public async ValueTask Validate(AuthorizeContext context, CancellationToken cancellationToken)
    {
        context.AssertHasClient();
        context.AssertHasResponseType();

        //////////////////////////////////////////////////////////
        // check if scopes are valid/supported and check for resource scopes
        //////////////////////////////////////////////////////////

        var resourcesFromStore = await resourceStore.FindResourcesByScopeAsync(context.RequestedScopes, true, cancellationToken);
        foreach (var scope in context.RequestedScopes)
        {
            if (!IsScopeValidAsync(context, context.Client, resourcesFromStore, scope))
                return;
        }

        if (context.Resources.IdentityResources.Count is not 0 && !context.IsOpenIdRequest)
        {
            logger.LogError(options, "Identity related scope requests, but no openid scope", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "Identity scopes requested, but openid scope is missing");
            return;
        }

        if (context.Resources.ApiScopes.Count is not 0)
        {
            context.IsApiResourceRequest = true;
        }

        //////////////////////////////////////////////////////////
        // check scope vs response_type plausibility
        //////////////////////////////////////////////////////////
        var requirement = Constants.ResponseTypeToScopeRequirement[context.ResponseType];
        var requireOpenId = requirement == Constants.ScopeRequirement.Identity
            || requirement == Constants.ScopeRequirement.IdentityOnly;
        if (requireOpenId && !context.IsOpenIdRequest)
        {
            logger.LogError(options, "The parameter response_type requires the openid scope", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "missing openid scope");
        }

        //////////////////////////////////////////////////////////
        // check id vs resource scopes and response types plausibility
        //////////////////////////////////////////////////////////
        var responseTypeValidationCheck = true;
        switch (requirement)
        {
            case Constants.ScopeRequirement.Identity:
                if (!context.Resources.IdentityResources.Any())
                {
                    logger.LogError(options, "Requests for id_token response type must include identity scopes", context);
                    responseTypeValidationCheck = false;
                }
                break;
            case Constants.ScopeRequirement.IdentityOnly:
                if (!context.Resources.IdentityResources.Any() || context.Resources.ApiScopes.Any())
                {
                    logger.LogError(options, "Requests for id_token response type only must not include resource scopes", context);
                    responseTypeValidationCheck = false;
                }
                break;
            case Constants.ScopeRequirement.ResourceOnly:
                if (context.Resources.IdentityResources.Any() || !context.Resources.ApiScopes.Any())
                {
                    logger.LogError(options, "Requests for token response type only must include resource scopes, but no identity scopes.", context);
                    responseTypeValidationCheck = false;
                }
                break;
        }

        if (!responseTypeValidationCheck)
        {
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "Invalid scope for response type");
            return;
        }
    }

    private bool IsScopeValidAsync(AuthorizeContext context, Client client, Resources resourcesFromStore, string requestedScope)
    {
        if (requestedScope == ServerConstants.StandardScopes.OfflineAccess)
        {
            if (client.AllowOfflineAccess)
            {
                context.Resources.OfflineAccess = true;
            }
            else
            {
                logger.LogError(options, "Offline access is not allowed for this client", $"{requestedScope}, {client.Id}, {client.Name}", context);
                context.InvalidRequest(OidcConstants.AuthorizeErrors.InvalidScope, "Offline access is not allowed for this client");
                return false;
            }
        }
        else
        {
            if (resourcesFromStore.TryFindIdentityResourceByName(requestedScope, out var identity))
            {
                if (client.AllowedScopes.Contains(identity.Name))
                {
                    context.Resources.IdentityResources.Add(identity);
                }
                else
                {
                    logger.LogError(options, "Identity Scope not allowed for the client", $"{requestedScope}, {client.Id}, {client.Name}", context);
                    context.InvalidRequest(OidcConstants.AuthorizeErrors.InvalidScope);
                    return false;
                }
            }
            else if (resourcesFromStore.TryFindApiScopeByName(requestedScope, out var apiScope))
            {
                if (client.AllowedScopes.Contains(apiScope.Name))
                {
                    context.Resources.ApiScopes.Add(apiScope);

                    var apis = resourcesFromStore.FindApiResourceByScopeName(apiScope.Name);
                    foreach (var api in apis)
                    {
                        context.Resources.ApiResources.Add(api);
                    }
                }
                else
                {
                    logger.LogError(options, "Api Scope not allowed for the client", $"{requestedScope}, {client.Id}, {client.Name}", context);
                    context.InvalidRequest(OidcConstants.AuthorizeErrors.InvalidScope);
                    return false;
                }
            }
            else
            {
                logger.LogError(options, "Scope not found in store", requestedScope, context);
                context.InvalidRequest(OidcConstants.AuthorizeErrors.InvalidScope);
                return false;
            }
        }

        return true;
    }
}