using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Scopes;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contexts.Validators;

#pragma warning disable S3267 // simplify linq

public class ResourcesValidator : IValidator<IWithResources>
{
    private readonly ILogger logger;

    public ResourcesValidator(ILogger<ResourcesValidator> logger)
    {
        this.logger = logger;
    }

    public ValueTask Validate(IWithResources context, CancellationToken ct)
    {
        logger.LogDebug("Start validation client allowed scopes");

        context.ClientParameters.AssertHasClient();

        var client = context.ClientParameters.Client;
        var resources = context.Scopes;

        if (!resources.IsValid)
        {
            logger.LogError(context, "Resources are not load or invalid", context.Scope);
            context.InvalidRequest(Oidc.Authorize.Errors.InvalidScope);
            return default;
        }

        if (resources.OfflineAccess)
        {
            if (client.AllowOfflineAccess)
            {
                context.Scopes.OfflineAccess = true;
            }
            else
            {
                logger.LogError(context, "Offline access is not allowed for this client", $"{client.Id}, {client.Name}");
                context.InvalidRequest(Oidc.Authorize.Errors.InvalidScope, "Offline access is not allowed for this client");
                return default;
            }
        }

        foreach(var identity in resources.IdentityScopes)
        {
            if (!client.AllowedIdentityScopes.Contains(identity.Name))
            {
                logger.LogError(context, "Identity Scope not allowed for the client", $"{identity.Name}, {client.Id}, {client.Name}");
                context.InvalidRequest(Oidc.Authorize.Errors.InvalidScope);
                return default;
            }
        }

        foreach(var scope in resources.Scopes)
        {
            // Scope names are globally unique within a realm (Fase 3), so the owner is the requested
            // resource server that exposes this scope.
            var owner = resources.ResourceServers.FirstOrDefault(rs => rs.Scopes.Any(s => s.Name == scope.Name));

            // The resource server must allow its scopes to be requested via the scope parameter (ADR-012).
            if (owner is not null && !owner.AllowScopeRequests)
            {
                logger.LogError(context, "Scope requests are not allowed for the resource server", $"{owner.Name}, {scope.Name}, {client.Id}");
                context.InvalidRequest(Oidc.Authorize.Errors.InvalidScope);
                return default;
            }

            if (!IsScopeAllowed(client, scope, owner))
            {
                logger.LogError(context, "Scope not allowed for the client", $"{scope.Name}, {client.Id}, {client.Name}");
                context.InvalidRequest(Oidc.Authorize.Errors.InvalidScope);
                return default;
            }

            // scope + resource coherence (ADR-012): if any resource indicator was requested and this scope's
            // resource server exposes protected resources, at least one of them must be among the requested ones.
            if (resources.ProtectedResources.Count is not 0
                && owner is not null && owner.ProtectedResources.Count is not 0
                && !owner.ProtectedResources.Any(pr => resources.ProtectedResources.Any(rp => rp.ResourceUri == pr.ResourceUri)))
            {
                logger.LogError(context, "Scope requires a matching resource indicator", $"{scope.Name}, {owner.Name}, {client.Id}");
                context.InvalidRequest(Oidc.Authorize.Errors.InvalidTarget, "scope requires a matching resource indicator");
                return default;
            }
        }

        // Resource indicator authorization (ADR-012): the client may only request a resource whose owning
        // resource server is in AllowedResourceServers (or with AllowAllResourceServers).
        foreach (var resource in resources.ProtectedResources)
        {
            var owner = resources.ResourceServers.FirstOrDefault(rs => rs.ProtectedResources.Any(pr => pr.ResourceUri == resource.ResourceUri));
            var allowed = client.AllowAllResourceServers
                || (owner is not null && client.AllowedResourceServers.Contains(owner.Name));

            if (!allowed)
            {
                logger.LogError(context, "Resource indicator not allowed for the client", $"{resource.ResourceUri}, {client.Id}, {client.Name}");
                context.InvalidRequest(Oidc.Authorize.Errors.InvalidTarget, "resource indicator not allowed for this client");
                return default;
            }
        }

        switch (context)
        {
            case AuthorizeContext ac:
                ac.ResourcesValidated();
                break;
            case ClientCredentialsContext cc:
                cc.ResourcesValidated();
                break;
        }

        return default;
    }

    /// <summary>
    /// A scope is allowed when the client has Full Scope Allowed, when the scope is listed
    /// individually in <see cref="Client.AllowedScopes"/>, or when its owning resource server is in
    /// <see cref="Client.AllowedResourceServers"/> (which authorizes all of its scopes).
    /// </summary>
    private static bool IsScopeAllowed(Client client, Scope scope, ResourceServer? owner)
    {
        if (client.AllowAllResourceServers)
            return true;

        if (client.AllowedScopes.Contains(scope.Name))
            return true;

        return owner is not null && client.AllowedResourceServers.Contains(owner.Name);
    }
}
