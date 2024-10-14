using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class AuthorizeMainValidator : IValidator<AuthorizeContext>
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public AuthorizeMainValidator(IOptions<ServerOptions> options, ILogger<AuthorizeMainValidator> logger)
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
        
        
        //////////////////////////////////////////////////////////
        // check if flow is allowed at authorize endpoint
        //////////////////////////////////////////////////////////
        if (!Constants.AllowedGrantTypesForAuthorizeEndpoint.Contains(grantType))
        {
            logger.LogError(options, "Invalid grant type", responseType, context);
            context.InvalidRequest("Invalid response_type");
            return;
        }

        context.GrantType = grantType;
        context.Items.GetOrCreate<Asserts>().HasGrantType = true;


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

        context.Items.GetOrCreate<Asserts>().HasResponseType = true;


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
        // check nonce
        //////////////////////////////////////////////////////////
        if (context.Nonce.IsPresent())
        {
            if (context.Nonce.Length > options.InputLengthRestrictions.Nonce)
            {
                logger.LogError(options, "Nonce too long", context);
                context.InvalidRequest("Invalid nonce", "too long");
            }
        }
        else if (context.GrantType == GrantType.Hybrid && context.IsOpenIdRequest)
        {
            logger.LogError(options, "Nonce required for implicit and hybrid flow with openid scope", context);
            context.InvalidRequest("Invalid nonce", "required");
        }


        //////////////////////////////////////////////////////////
        // check prompt
        //////////////////////////////////////////////////////////
        if (context.PromptModes.Count > 1 && context.PromptModes.Contains(PromptModes.None))
        {
            logger.LogError(options, "The property prompt contains 'none' and other values. 'none' should be used by itself.", context);
            context.InvalidRequest("Invalid prompt");
        }


        //////////////////////////////////////////////////////////
        // check ui locales
        //////////////////////////////////////////////////////////
        if (context.UiLocales.IsPresent() && context.UiLocales.Length > options.InputLengthRestrictions.UiLocale)
        {
            logger.LogError(options, "UI locale too long", context);
            context.InvalidRequest("Invalid ui_locales");
        }


        //////////////////////////////////////////////////////////
        // check login_hint
        //////////////////////////////////////////////////////////
        if (context.LoginHint.IsPresent() && context.LoginHint.Length > options.InputLengthRestrictions.LoginHint)
        {
            logger.LogError(options, "Login hint too long", context);
            context.InvalidRequest("Invalid login_hint", "too long");
        }

        //////////////////////////////////////////////////////////
        // check acr_values
        //////////////////////////////////////////////////////////
        ////////////////////////////////////////.AcrValues//////////////////
        if (context.AuthenticationContextReferenceClasses.Count > 0)
        {
            var acrValues = context.Raw.Get(AuthorizeRequest.AcrValues)!;
            if (acrValues.Length > options.InputLengthRestrictions.AcrValues)
            {
                logger.LogError(options, "Acr values too long", context);
                context.InvalidRequest("Invalid acr_values", "too long");
            }
        }

        //////////////////////////////////////////////////////////
        // check custom acr_values: idp
        //////////////////////////////////////////////////////////
        var idp = context.GetIdP();
        if (idp.IsPresent()
            && context.Client.IdentityProviderRestrictions.Count is not 0
            && !context.Client.IdentityProviderRestrictions.Contains(idp))
        {
            logger.LogError(options, "The parameter idp requested is not in client restriction list", idp, context);
            context.RemoveIdP();
        }

        //////////////////////////////////////////////////////////////
        ////// check session cookie
        //////////////////////////////////////////////////////////////
        ////if (options.Endpoints.EnableCheckSessionEndpoint)
        ////{
        ////    var sessionId = await userSession.GetSessionIdAsync();
        ////    if (sessionId.IsPresent())
        ////    {
        ////        context.SessionId = sessionId;
        ////    }
        ////    else
        ////    {
        ////        LogError("Check session endpoint enabled, but SessionId is missing", request);
        ////    }
        ////}
    }
}
