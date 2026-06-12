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

        foreach(var apiScope in resources.Scopes)
        {
            if (!IsApiScopeAllowed(client, apiScope, resources))
            {
                logger.LogError(context, "Api Scope not allowed for the client", $"{apiScope.Name}, {client.Id}, {client.Name}");
                context.InvalidRequest(Oidc.Authorize.Errors.InvalidScope);
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
    /// An API scope is allowed when the client has Full Scope Allowed, when the scope is listed
    /// individually in <see cref="Client.AllowedScopes"/>, or when its owning resource server is in
    /// <see cref="Client.AllowedResourceServers"/> (which authorizes all of its scopes).
    /// </summary>
    private static bool IsApiScopeAllowed(Client client, Scope scope, RequestedResources resources)
    {
        if (client.AllowAllResourceServers)
            return true;

        if (client.AllowedScopes.Contains(scope.Name))
            return true;

        // Scope names are globally unique within a realm (Fase 3), so the owner is the requested
        // resource server that exposes this scope.
        var owner = resources.ResourceServers.FirstOrDefault(rs => rs.Scopes.Any(s => s.Name == scope.Name));
        return owner is not null && client.AllowedResourceServers.Contains(owner.Name);
    }
}
