using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Decorators;

public class ClientResourceDecorator : IDecorator<ClientCredentialsContext>
{
    private readonly IStorage storage;
    private readonly ILogger logger;

    public ClientResourceDecorator(IStorage storage, ILogger<ClientResourceDecorator> logger)
    {
        this.storage = storage;
        this.logger = logger;
    }

    public async Task Decorate(ClientCredentialsContext context, Func<Task> next, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClient();

        var client = context.ClientParameters.Client;

        IEnumerable<string>? scopes;

        if (context.Scope.IsPresent())
        {
            scopes = context.Scope.FromSpaceSeparatedString();
        }
        else if (client.AllowedScopes.Count != 0)
            scopes = client.AllowedScopes;
        else
            scopes = null;

        if (scopes is not null)
        {
            var resourceStore = storage.GetResourceStore(context.Realm);
            var resourcesFromStore = await resourceStore.FindResourcesByScopeAsync(scopes, true, ct);
            if (resourcesFromStore.MissingScopes.Count is not 0)
            {
                logger.LogError(context, "Requested scopes are invalid or inactive: {Scopes}", string.Join(" ", resourcesFromStore.MissingScopes));
                context.InvalidRequest(TokenErrors.InvalidScope, "scopes requested are invalid or inactive");
                return;
            }

            if (resourcesFromStore.IdentityResources.Count is not 0)
            {
                logger.LogError(context, "Client cannot request OpenID scopes in client credentials flow");
                context.InvalidRequest(TokenErrors.InvalidScope, "scopes requested are invalid or inactive");
                return;
            }

            if (resourcesFromStore.OfflineAccess)
            {
                logger.LogError(context, "Client cannot request a refresh token in client credentials flow");
                context.InvalidRequest(TokenErrors.InvalidScope, "scopes requested are invalid or inactive");
            }

            resourcesFromStore.CopyTo(context.Resources);
        }

        await next();
    }
}
