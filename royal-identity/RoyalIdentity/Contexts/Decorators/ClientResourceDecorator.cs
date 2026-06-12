using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Pipelines.Abstractions;

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

        var resourceUris = context.Scopes.RequestedResourceUris;

        IEnumerable<string>? scopes;
        if (context.Scope.IsPresent())
        {
            scopes = context.Scope.FromSpaceSeparatedString();
        }
        else if (resourceUris.Count is 0)
        {
            // No explicit scope and no resource: default to the API scopes the client is allowed to request
            // (AllowedScopes + scopes of AllowedResourceServers, or all when AllowAllResourceServers).
            scopes = await GetDefaultClientScopesAsync(client, context.Realm, ct);
        }
        else
        {
            // Resource-only request (audience without scopes).
            scopes = [];
        }

        var resourceStore = storage.GetResourceStore(context.Realm);
        var resourcesFromStore = await resourceStore.FindRequestedResourcesAsync(scopes ?? [], resourceUris, true, ct);

        if (resourcesFromStore.HasInvalidTargets)
        {
            logger.LogError(context, "Requested resource indicators are invalid: {Resources}", resourcesFromStore.GetInvalidTargets());
            context.InvalidRequest(Oidc.Token.Errors.InvalidTarget, "resource indicators requested are invalid");
            return;
        }

        if (resourcesFromStore.MissingScopes.Count is not 0)
        {
            logger.LogError(context, "Requested scopes are invalid or inactive: {Scopes}", string.Join(" ", resourcesFromStore.MissingScopes));
            context.InvalidRequest(Oidc.Token.Errors.InvalidScope, "scopes requested are invalid or inactive");
            return;
        }

        if (resourcesFromStore.IdentityScopes.Count is not 0)
        {
            logger.LogError(context, "Client cannot request OpenID scopes in client credentials flow");
            context.InvalidRequest(Oidc.Token.Errors.InvalidScope, "scopes requested are invalid or inactive");
            return;
        }

        if (resourcesFromStore.OfflineAccess)
        {
            logger.LogError(context, "Client cannot request a refresh token in client credentials flow");
            context.InvalidRequest(Oidc.Token.Errors.InvalidScope, "scopes requested are invalid or inactive");
            return;
        }

        resourcesFromStore.CopyTo(context.Scopes);

        await next();
    }

    private async Task<IEnumerable<string>?> GetDefaultClientScopesAsync(Client client, Realm realm, CancellationToken ct)
    {
        // Mirrors the authorization rule (ResourcesValidator): only scopes the client could actually obtain.
        var all = await storage.GetResourceStore(realm).GetAllEnabledResourcesAsync(ct);

        var names = all.ResourceServers
            .Where(rs => rs.AllowScopeRequests)
            .SelectMany(rs => rs.Scopes.Select(scope => (rs, scope)))
            .Where(x => client.AllowAllResourceServers
                || client.AllowedScopes.Contains(x.scope.Name)
                || client.AllowedResourceServers.Contains(x.rs.Name))
            .Select(x => x.scope.Name)
            .Distinct()
            .ToList();

        return names.Count > 0 ? names : null;
    }
}
