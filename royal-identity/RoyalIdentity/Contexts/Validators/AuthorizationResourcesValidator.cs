using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class AuthorizationResourcesValidator : IValidator<IAuthorizationContextBase>
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public AuthorizationResourcesValidator(
        IOptions<ServerOptions> options,
        ILogger<AuthorizationResourcesValidator> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public ValueTask Validate(IAuthorizationContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start validating requested resources.");

        context.ClientParameters.AssertHasClient();

        //////////////////////////////////////////////////////////
        // check scope vs response_type plausibility
        //////////////////////////////////////////////////////////
        if (context.ResponseTypes.Contains(ResponseTypes.IdToken) && !context.Resources.IsOpenId)
        {
            logger.LogError(options, "The parameter response_type requires the openid scope", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "missing openid scope");
            return default;
        }

        if (context.ResponseTypes.Only(ResponseTypes.IdToken) &&
            (context.Resources.ApiScopes.Any() || context.Resources.ApiResources.Any()))
        {
            logger.LogError(options, "Requests for id_token response type only must include identity scopes only", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "resource scopes are not allowed for id_token response type only");
            return default;
        }

        if (context.ResponseTypes.Contains(ResponseTypes.Token) && !context.Resources.ApiScopes.Any())
        {
            logger.LogError(options, "The parameter response_type requires resource scopes", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "missing resource scopes");
            return default;
        }

        if (context.ResponseTypes.Only(ResponseTypes.Token) && context.Resources.IdentityResources.Any())
        {
            logger.LogError(options, "Requests for token response type only must include resource scopes only", context);
        }

        return default;
    }
}