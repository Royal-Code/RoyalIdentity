using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Decorators;

public class ResourcesDecorator : IDecorator<IWithResources>
{
    private readonly ServerOptions options;
    private readonly IResourceStore resourceStore;
    private readonly ILogger logger;

    public ResourcesDecorator(
        IOptions<ServerOptions> options,
        IResourceStore resourceStore,
        ILogger<ResourcesDecorator> logger)
    {
        this.options = options.Value;
        this.resourceStore = resourceStore;
        this.logger = logger;
    }

    public async Task Decorate(IWithResources context, Func<Task> next, CancellationToken ct)
    {
        context.AssertHasClient();

        logger.LogDebug("Start processing requested resources.");

        //////////////////////////////////////////////////////////
        // check if scopes are valid/supported and check for resource scopes
        //////////////////////////////////////////////////////////

        var resourcesFromStore = await resourceStore.FindResourcesByScopeAsync(context.RequestedScopes, true, ct);
        foreach (var scope in context.RequestedScopes)
        {
            if (!IsScopeValidAsync(context, context.Client, resourcesFromStore, scope))
                return;
        }


        //////////////////////////////////////////////////////////
        // once the requested scopes are validated, we can copy the resources to the context
        //////////////////////////////////////////////////////////
        resourcesFromStore.CopyTo(context.Resources);


        //////////////////////////////////////////////////////////
        // check for openid scope
        //////////////////////////////////////////////////////////
        
        context.IsOpenIdRequest = context.Resources.IsOpenId;
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

        await next();
    }

    private bool IsScopeValidAsync(IWithResources context, Client client, Resources resourcesFromStore, string requestedScope)
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
                context.InvalidRequest(AuthorizeErrors.InvalidScope, "Offline access is not allowed for this client");
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
                    context.InvalidRequest(AuthorizeErrors.InvalidScope);
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
                    context.InvalidRequest(AuthorizeErrors.InvalidScope);
                    return false;
                }
            }
            else
            {
                logger.LogError(options, "Scope not found in store", requestedScope, context);
                context.InvalidRequest(AuthorizeErrors.InvalidScope);
                return false;
            }
        }

        return true;
    }
}
