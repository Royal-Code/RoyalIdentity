using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Handlers;

public class ProtectedResourceMetadataHandler : IHandler<ProtectedResourceMetadataContext>
{
    private readonly IStorage storage;
    private readonly ILogger logger;

    public ProtectedResourceMetadataHandler(
        IStorage storage,
        ILogger<ProtectedResourceMetadataHandler> logger)
    {
        this.storage = storage;
        this.logger = logger;
    }

    public async Task Handle(ProtectedResourceMetadataContext context, CancellationToken ct)
    {
        logger.LogDebug("Handle protected resource metadata context start");

        var resources = await storage.GetResourceStore(context.Realm).FindRequestedResourcesAsync(
            [],
            [context.ResourceUri],
            true,
            ct);

        if (resources.HasInvalidTargets || resources.ProtectedResources.Count is 0)
        {
            logger.LogError("Protected resource metadata requested for invalid resource: {Resource}", context.ResourceUri);
            context.Error(Oidc.Token.Errors.InvalidTarget, "resource indicator requested is invalid");
            return;
        }

        var resource = resources.ProtectedResources.Single();
        var owner = resources.ResourceServers
            .Single(rs => rs.ProtectedResources.Any(pr => pr.ResourceUri == resource.ResourceUri));

        var entries = new Dictionary<string, object>
        {
            [Oidc.ProtectedResource.Metadata.Resource] = resource.ResourceUri,
            [Oidc.ProtectedResource.Metadata.AuthorizationServers] = new[] { context.IssuerUri },
            [Oidc.ProtectedResource.Metadata.BearerMethodsSupported] = new[]
            {
                Oidc.ProtectedResource.BearerMethods.Header,
                Oidc.ProtectedResource.BearerMethods.Body
            },
            [Oidc.ProtectedResource.Metadata.ResourceName] = resource.DisplayName ?? owner.DisplayName,
        };

        if (owner.AllowScopeRequests)
        {
            var scopesSupported = owner.Scopes
                .Where(scope => scope.Enabled && scope.ShowInDiscoveryDocument)
                .Select(scope => scope.Name)
                .ToArray();

            if (scopesSupported.Length is not 0)
                entries[Oidc.ProtectedResource.Metadata.ScopesSupported] = scopesSupported;
        }

        if (resource.DocumentationUri.IsPresent())
            entries[Oidc.ProtectedResource.Metadata.ResourceDocumentation] = resource.DocumentationUri;

        if (resource.PolicyUri.IsPresent())
            entries[Oidc.ProtectedResource.Metadata.ResourcePolicyUri] = resource.PolicyUri;

        if (resource.TosUri.IsPresent())
            entries[Oidc.ProtectedResource.Metadata.ResourceTosUri] = resource.TosUri;

        context.Response = new DiscoveryResponse(entries);
    }
}
