using Microsoft.Extensions.Logging;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Validators;

public class AuthorizationResourcesValidator : IValidator<IAuthorizationContextBase>
{
    private readonly ILogger logger;

    public AuthorizationResourcesValidator(ILogger<AuthorizationResourcesValidator> logger)
    {
        this.logger = logger;
    }

    public ValueTask Validate(IAuthorizationContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start validating requested resources.");

        context.ClientParameters.AssertHasClient();

        //////////////////////////////////////////////////////////
        // check scope vs response_type plausibility
        //////////////////////////////////////////////////////////
        if (context.ResponseTypes.Contains(Oidc.ResponseTypes.IdToken) && !context.Scopes.IsOpenId)
        {
            logger.LogError(context, "The parameter response_type requires the openid scope");
            context.InvalidRequest(Oidc.Authorize.Errors.InvalidScope, "missing openid scope");
            return default;
        }

        if (context.ResponseTypes.Only(Oidc.ResponseTypes.IdToken) &&
            (context.Scopes.Scopes.Any() || context.Scopes.ResourceServers.Any()))
        {
            logger.LogError(context, "Requests for id_token response type only must include identity scopes only");
            context.InvalidRequest(Oidc.Authorize.Errors.InvalidScope, "resource scopes are not allowed for id_token response type only");
            return default;
        }

        if (context.ResponseTypes.Contains(Oidc.ResponseTypes.Token) && !context.Scopes.Scopes.Any())
        {
            logger.LogError(context, "The parameter response_type requires resource scopes");
            context.InvalidRequest(Oidc.Authorize.Errors.InvalidScope, "missing resource scopes");
            return default;
        }

        if (context.ResponseTypes.Only(Oidc.ResponseTypes.Token) && context.Scopes.IdentityScopes.Any())
        {
            logger.LogError(context, "Requests for token response type only must include resource scopes only");
        }

        return default;
    }
}