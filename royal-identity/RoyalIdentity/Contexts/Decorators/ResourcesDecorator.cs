using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

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
        var scopesFromStorage = await resourceStore.FindResourcesByScopeAsync(context.Scopes.Scopes, true, ct);
        if (!scopesFromStorage.IsValid)
        {
            logger.LogError(context, "Requested scopes are invalid or inactive: {Scopes}", context.Scopes.GetInvalidScopes());
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "scopes requested are invalid or inactive");
            return;
        }

        //////////////////////////////////////////////////////////
        // once the requested scopes are validated, we can copy the resources to the context
        //////////////////////////////////////////////////////////
        scopesFromStorage.CopyTo(context.Scopes);


        //////////////////////////////////////////////////////////
        // check for openid scope
        //////////////////////////////////////////////////////////
        if (!context.Scopes.IsOpenId && 
            context.Scopes.IdentityResources.Count is not 0)
        {
            logger.LogError(context, "Identity related scope requests, but no openid scope");
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "Identity scopes requested, but openid scope is missing");
            return;
        }

        await next();
    }
}
