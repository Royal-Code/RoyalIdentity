using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class AuthorizeValidator : IValidator<AuthorizeContext>
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public AuthorizeValidator(IOptions<ServerOptions> options, ILogger<AuthorizeValidator> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public async ValueTask Validate(AuthorizeContext context, CancellationToken cancellationToken)
    {
        context.AssertHasClient();

        //////////////////////////////////////////////////////////
        // response_type must be present and supported
        //////////////////////////////////////////////////////////
        var responseType = context.ResponseType;
        if (responseType.IsMissing())
        {
            logger.LogError(options, "Missing response_type", context);
            context.InvalidRequest(AuthorizeErrors.UnsupportedResponseType, "Missing response_type");
            return;
        }

        // The responseType may come in in an unconventional order.
        // Use an IEqualityComparer that doesn't care about the order of multiple values.
        // Per https://tools.ietf.org/html/rfc6749#section-3.1.1 -
        // 'Extension response types MAY contain a space-delimited (%x20) list of
        // values, where the order of values does not matter (e.g., response
        // type "a b" is the same as "b a").'
        // http://openid.net/specs/oauth-v2-multiple-response-types-1_0-03.html#terminology -
        // 'If a response type contains one of more space characters (%20), it is compared
        // as a space-delimited list of values in which the order of values does not matter.'
        if (!Constants.SupportedResponseTypes.Contains(responseType, ResponseTypeEqualityComparer.Instance))
        {
            logger.LogError(options, "Response type not supported", responseType, context);
            context.InvalidRequest(AuthorizeErrors.UnsupportedResponseType, "Response type not supported");
            return;
        }

        // Even though the responseType may have come in in an unconventional order,
        // we still need the request's ResponseType property to be set to the
        // conventional, supported response type.
        context.ResponseType = Constants.SupportedResponseTypes.First(
            supportedResponseType => ResponseTypeEqualityComparer.Instance.Equals(supportedResponseType, responseType));


        //////////////////////////////////////////////////////////
        // match response_type to grant type
        //////////////////////////////////////////////////////////
        var grantType = Constants.ResponseTypeToGrantTypeMapping[context.ResponseType];
        context.GrantType = grantType;
        context.Items.GetOrCreate<Asserts>().HasGrantType = true;

        //////////////////////////////////////////////////////////
        // check if flow is allowed at authorize endpoint
        //////////////////////////////////////////////////////////
        if (!Constants.AllowedGrantTypesForAuthorizeEndpoint.Contains(grantType))
        {
            logger.LogError(options, "Invalid grant type", responseType, context);
            context.InvalidRequest("Invalid response_type");
            return;
        }

        //////////////////////////////////////////////////////////
        // check response_mode parameter and set response_mode
        //////////////////////////////////////////////////////////

        // check if response_mode parameter is present and valid
        var responseMode = context.ResponseMode;
        if (responseMode.IsPresent())
        {
            if (Constants.SupportedResponseModes.Contains(responseMode))
            {
                if (!Constants.AllowedResponseModesForGrantType[grantType].Contains(responseMode))
                {
                    logger.LogError(options, "Invalid response_mode for response_type", responseMode, context);
                    context.InvalidRequest(AuthorizeErrors.InvalidRequest, "Invalid response_mode for response_type");
                    return;
                }
            }
            else
            {
                logger.LogError(options, "Unsupported response_mode", responseMode, context);
                context.InvalidRequest(AuthorizeErrors.UnsupportedResponseType);
                return;
            }
        }


        //////////////////////////////////////////////////////////
        // check if grant type is allowed for client
        //////////////////////////////////////////////////////////
        if (!context.Client.AllowedGrantTypes.Contains(grantType))
        {
            logger.LogError(options, "Invalid grant type for client", grantType, context);
            context.InvalidRequest(AuthorizeErrors.UnauthorizedClient, "Invalid grant type for client");
            return;
        }

        //////////////////////////////////////////////////////////
        // check if response type contains an access token,
        // and if client is allowed to request access token via browser
        //////////////////////////////////////////////////////////
        var responseTypes = responseType.FromSpaceSeparatedString();
        if (responseTypes.Contains(ResponseTypes.Token) && !context.Client.AllowAccessTokensViaBrowser)
        {
            logger.LogError(
                options,
                "Client requested access token - but client is not configured to receive access tokens via browser",
                context);
            context.InvalidRequest(
                AuthorizeErrors.UnauthorizedClient,
                "Client not configured to receive access tokens via browser");
            return;
        }


        //////////////////////////////////////////////////////////
        // scope must be present
        //////////////////////////////////////////////////////////
        if (context.RequestedScopes.Count is 0)
        {
            logger.LogError(options, "scope is missing", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope);
            return;
        }

        if (context.RequestedScopes.Count > options.InputLengthRestrictions.Scope)
        {
            logger.LogError(options, "Scopes too long", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "scopes too long");
            return;
        }

        if (context.RequestedScopes.Contains(ServerConstants.StandardScopes.OpenId))
        {
            context.IsOpenIdRequest = true;
        }

        //////////////////////////////////////////////////////////
        // check scope vs response_type plausability
        //////////////////////////////////////////////////////////
        var requirement = Constants.ResponseTypeToScopeRequirement[context.ResponseType];
        var requireOpenId = requirement == Constants.ScopeRequirement.Identity
            || requirement == Constants.ScopeRequirement.IdentityOnly;
        if (requireOpenId && !context.IsOpenIdRequest)
        {
            logger.LogError(options, "The parameter response_type requires the openid scope", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "missing openid scope");
            return;
        }

        //////////////////////////////////////////////////////////
        // check if scopes are valid/supported and check for resource scopes
        //////////////////////////////////////////////////////////
        //var validatedResources = await _resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        //{
        //    Client = request.Client,
        //    Scopes = request.RequestedScopes
        //});

        //if (!validatedResources.Succeeded)
        //{
        //    return Invalid(request, OidcConstants.AuthorizeErrors.InvalidScope, "Invalid scope");
        //}

        //if (validatedResources.Resources.IdentityResources.Any() && !request.IsOpenIdRequest)
        //{
        //    LogError("Identity related scope requests, but no openid scope", request);
        //    return Invalid(request, OidcConstants.AuthorizeErrors.InvalidScope, "Identity scopes requested, but openid scope is missing");
        //}

        //if (validatedResources.Resources.ApiScopes.Any())
        //{
        //    request.IsApiResourceRequest = true;
        //}

        //////////////////////////////////////////////////////////
        // check id vs resource scopes and response types plausability
        //////////////////////////////////////////////////////////
        var responseTypeValidationCheck = true;
        switch (requirement)
        {
            case Constants.ScopeRequirement.Identity:
                if (!validatedResources.Resources.IdentityResources.Any())
                {
                    _logger.LogError("Requests for id_token response type must include identity scopes");
                    responseTypeValidationCheck = false;
                }
                break;
            case Constants.ScopeRequirement.IdentityOnly:
                if (!validatedResources.Resources.IdentityResources.Any() || validatedResources.Resources.ApiScopes.Any())
                {
                    _logger.LogError("Requests for id_token response type only must not include resource scopes");
                    responseTypeValidationCheck = false;
                }
                break;
            case Constants.ScopeRequirement.ResourceOnly:
                if (validatedResources.Resources.IdentityResources.Any() || !validatedResources.Resources.ApiScopes.Any())
                {
                    _logger.LogError("Requests for token response type only must include resource scopes, but no identity scopes.");
                    responseTypeValidationCheck = false;
                }
                break;
        }

        if (!responseTypeValidationCheck)
        {
            return Invalid(request, AuthorizeErrors.InvalidScope, "Invalid scope for response type");
        }

        request.ValidatedResources = validatedResources;
    }
}
