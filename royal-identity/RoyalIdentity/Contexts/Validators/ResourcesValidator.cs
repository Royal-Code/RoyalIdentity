using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

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
        var resources = context.Resources;

        if (!resources.IsValid)
        {
            logger.LogError(context, "Resources are not load or invalid", context.Scope);
            context.InvalidRequest(AuthorizeErrors.InvalidScope);
            return default;
        }

        if (resources.OfflineAccess)
        {
            if (client.AllowOfflineAccess)
            {
                context.Resources.OfflineAccess = true;
            }
            else
            {
                logger.LogError(context, "Offline access is not allowed for this client", $"{client.Id}, {client.Name}");
                context.InvalidRequest(AuthorizeErrors.InvalidScope, "Offline access is not allowed for this client");
                return default;
            }
        }

        foreach(var identity in resources.IdentityResources)
        {
            if (!client.AllowedScopes.Contains(identity.Name))
            {
                logger.LogError(context, "Identity Scope not allowed for the client", $"{identity.Name}, {client.Id}, {client.Name}");
                context.InvalidRequest(AuthorizeErrors.InvalidScope);
                return default;
            }
        }

        foreach(var apiScope in resources.ApiScopes)
        {
            if (!client.AllowedScopes.Contains(apiScope.Name))
            {
                logger.LogError(context, "Api Scope not allowed for the client", $"{apiScope.Name}, {client.Id}, {client.Name}");
                context.InvalidRequest(AuthorizeErrors.InvalidScope);
                return default;
            }
        }

        context.ResourcesValidated();

        return default;
    }
}
