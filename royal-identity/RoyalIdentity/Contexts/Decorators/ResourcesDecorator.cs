using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Decorators;

public class ResourcesDecorator : IDecorator<IWithResources>
{
    private readonly IStorage storage;
    private readonly ILogger logger;

    public ResourcesDecorator(IStorage storage, ILogger<ResourcesDecorator> logger)
    {
        this.storage = storage;
        this.logger = logger;
    }

    public async Task Decorate(IWithResources context, Func<Task> next, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClient();

        logger.LogDebug("Start processing requested resources.");

        //////////////////////////////////////////////////////////
        // check if scopes are valid/supported and check for resource scopes
        //////////////////////////////////////////////////////////

        var resourceStore = storage.GetResourceStore(context.Realm);
        var resourcesFromStore = await resourceStore.FindResourcesByScopeAsync(context.Resources.RequestedScopes, true, ct);
        if (resourcesFromStore.MissingScopes.Count is not 0)
        {
            logger.LogError(context, "Requested scopes are invalid or inactive: {Scopes}", string.Join(" ", resourcesFromStore.MissingScopes));
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "scopes requested are invalid or inactive");
            return;
        }

        //////////////////////////////////////////////////////////
        // once the requested scopes are validated, we can copy the resources to the context
        //////////////////////////////////////////////////////////
        resourcesFromStore.CopyTo(context.Resources);


        //////////////////////////////////////////////////////////
        // check for openid scope
        //////////////////////////////////////////////////////////
        if (!context.Resources.IsOpenId && 
            context.Resources.IdentityResources.Count is not 0)
        {
            logger.LogError(context, "Identity related scope requests, but no openid scope");
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "Identity scopes requested, but openid scope is missing");
            return;
        }

        await next();
    }
}
