using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class RequestedResourcesValidator : IValidator<IAuthorizationContextBase>
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public RequestedResourcesValidator(
        IOptions<ServerOptions> options,
        ILogger<RequestedResourcesValidator> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public ValueTask Validate(IAuthorizationContextBase context, CancellationToken ct)
    {
        context.AssertHasClient();

        //////////////////////////////////////////////////////////
        // check scope vs response_type plausibility
        //////////////////////////////////////////////////////////
        var requirement = Constants.GetResponseTypeScopeRequirement(context.ResponseTypes);
        var requireOpenId = requirement == Constants.ScopeRequirement.Identity
            || requirement == Constants.ScopeRequirement.IdentityOnly;
        if (requireOpenId && !context.IsOpenIdRequest)
        {
            logger.LogError(options, "The parameter response_type requires the openid scope", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "missing openid scope");
            return default;
        }


        //////////////////////////////////////////////////////////
        // check id vs resource scopes and response types plausibility
        //////////////////////////////////////////////////////////
        var responseTypeValidationCheck = true;
        switch (requirement)
        {
            case Constants.ScopeRequirement.Identity:
                if (!context.Resources.IdentityResources.Any())
                {
                    logger.LogError(options, "Requests for id_token response type must include identity scopes", context);
                    responseTypeValidationCheck = false;
                }
                break;
            case Constants.ScopeRequirement.IdentityOnly:
                if (!context.Resources.IdentityResources.Any() || context.Resources.ApiScopes.Any())
                {
                    logger.LogError(options, "Requests for id_token response type only must not include resource scopes", context);
                    responseTypeValidationCheck = false;
                }
                break;
            case Constants.ScopeRequirement.ResourceOnly:
                if (context.Resources.IdentityResources.Any() || !context.Resources.ApiScopes.Any())
                {
                    logger.LogError(options, "Requests for token response type only must include resource scopes, but no identity scopes.", context);
                    responseTypeValidationCheck = false;
                }
                break;
        }

        if (!responseTypeValidationCheck)
        {
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "Invalid scope for response type");
        }

        return default;
    }
}