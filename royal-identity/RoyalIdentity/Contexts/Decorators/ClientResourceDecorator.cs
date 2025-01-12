using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class ClientResourceDecorator : IDecorator<ClientCredentialsContext>
{
    private readonly IResourceStore resourceStore;
    private readonly ILogger logger;

    public ClientResourceDecorator(IResourceStore resourceStore, ILogger<ClientResourceDecorator> logger)
    {
        this.resourceStore = resourceStore;
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
            var resourcesFromStore = await resourceStore.FindResourcesByScopeAsync(scopes, true, ct);
            resourcesFromStore.CopyTo(context.Resources);
        }

        await next();
    }
}
