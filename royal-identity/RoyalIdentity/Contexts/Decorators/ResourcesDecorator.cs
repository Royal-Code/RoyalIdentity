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


        //////////////////////////////////////////////////////////
        // once the requested scopes are validated, we can copy the resources to the context
        //////////////////////////////////////////////////////////
        resourcesFromStore.CopyTo(context.Resources);


        //////////////////////////////////////////////////////////
        // check for openid scope
        //////////////////////////////////////////////////////////
        if (!context.Resources.IsOpenId && context.Resources.IdentityResources.Count is not 0)
        {
            logger.LogError(options, "Identity related scope requests, but no openid scope", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "Identity scopes requested, but openid scope is missing");
            return;
        }

        await next();
    }
}
