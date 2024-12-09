using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class AuthorizeMainValidator : IValidator<IAuthorizationContextBase>
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public AuthorizeMainValidator(IOptions<ServerOptions> options, ILogger<AuthorizeMainValidator> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public ValueTask Validate(IAuthorizationContextBase context, CancellationToken ct)
    {
        context.AssertHasClient();


        ////////////////////////////////////////////////////////////////////////////
        // response_type must be present and supported and allowed for the client
        ////////////////////////////////////////////////////////////////////////////
        var responseTypes = context.ResponseTypes;
        if (responseTypes.Count is 0)
        {
            logger.LogError(options, "Missing response_type", context);
            context.InvalidRequest(AuthorizeErrors.UnsupportedResponseType, "Missing response_type");
            return ValueTask.CompletedTask;
        }

        if (!Constants.ResponseTypesIsSuported(responseTypes))
        {
            logger.LogError(options, "Response type not supported", responseTypes.ToSpaceSeparatedString(), context);
            context.InvalidRequest(AuthorizeErrors.UnsupportedResponseType, "Response type not supported");
            return ValueTask.CompletedTask;
        }

        if (!responseTypes.All(context.Client.AllowedResponseTypes.Contains))
        {
            logger.LogError(
                options, 
                "Response type not allowed for the client",
                $"{responseTypes.ToSpaceSeparatedString()} - {context.Client.Id} - {context.Client.Name}",
                context);
            context.InvalidRequest(AuthorizeErrors.UnsupportedResponseType, "Response type not allowed");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check response_mode parameter and set response_mode
        //////////////////////////////////////////////////////////

        // check if response_mode parameter is present and valid
        var responseMode = context.ResponseMode;
        if (responseMode.IsPresent())
        {
            if (!Constants.SupportedResponseModes.Contains(responseMode))
            {
                logger.LogError(options, "Unsupported response_mode", responseMode, context);
                context.InvalidRequest(AuthorizeErrors.UnsupportedResponseType);
                return ValueTask.CompletedTask;
            }

            // when a token is required, the response mode should be form_post
            if (responseMode != ResponseModes.FormPost && responseTypes.Any(t => t != ResponseTypes.Code))
            {
                logger.LogError(
                    options,
                    "Invalid response_mode for response_type",
                    $"{responseMode} - {responseTypes.ToSpaceSeparatedString()}",
                    context);

                context.InvalidRequest(AuthorizeErrors.InvalidRequest, "Invalid response_mode for response_type");

                return ValueTask.CompletedTask;
            }            
        }


        //////////////////////////////////////////////////////////
        // scope must be present
        //////////////////////////////////////////////////////////
        if (context.RequestedScopes.Count is 0)
        {
            logger.LogError(options, "scope is missing", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope);
            return ValueTask.CompletedTask;
        }

        if (context.RequestedScopes.Count > options.InputLengthRestrictions.Scope)
        {
            logger.LogError(options, "Scopes too long", context);
            context.InvalidRequest(AuthorizeErrors.InvalidScope, "scopes too long");
            return ValueTask.CompletedTask;
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
                return ValueTask.CompletedTask;
            }
        }
        else if (context.IsOpenIdRequest)
        {
            logger.LogError(options, "Nonce required for implicit flow with openid scope", context);
            context.InvalidRequest("Invalid nonce", "required");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check prompt
        //////////////////////////////////////////////////////////
        if (context.PromptModes.Count > 1 && context.PromptModes.Contains(PromptModes.None))
        {
            logger.LogError(options, "The property prompt contains 'none' and other values. 'none' should be used by itself.", context);
            context.InvalidRequest("Invalid prompt");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check ui locales
        //////////////////////////////////////////////////////////
        if (context.UiLocales.IsPresent() && context.UiLocales.Length > options.InputLengthRestrictions.UiLocale)
        {
            logger.LogError(options, "UI locale too long", context);
            context.InvalidRequest("Invalid ui_locales");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check login_hint
        //////////////////////////////////////////////////////////
        if (context.LoginHint.IsPresent() && context.LoginHint.Length > options.InputLengthRestrictions.LoginHint)
        {
            logger.LogError(options, "Login hint too long", context);
            context.InvalidRequest("Invalid login_hint", "too long");
            return ValueTask.CompletedTask;
        }

        //////////////////////////////////////////////////////////
        // check acr_values
        //////////////////////////////////////////////////////////
        if (context.AcrValues.Count > 0)
        {
            var acrValues = context.Raw.Get(AuthorizeRequest.AcrValues)!;
            if (acrValues.Length > options.InputLengthRestrictions.AcrValues)
            {
                logger.LogError(options, "Acr values too long", context);
                context.InvalidRequest("Invalid acr_values", "too long");
                return ValueTask.CompletedTask;
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

        return ValueTask.CompletedTask;
    }
}
