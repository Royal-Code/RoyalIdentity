using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Validators;

public class AuthorizeMainValidator : IValidator<IAuthorizationContextBase>
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public AuthorizeMainValidator(IStorage storage, ILogger<AuthorizeMainValidator> logger)
    {
        options = storage.ServerOptions;

        this.logger = logger;
    }

    public ValueTask Validate(IAuthorizationContextBase context, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClient();
        var client = context.ClientParameters.Client;

        ////////////////////////////////////////////////////////////////////////////
        // response_type must be present and supported and allowed for the client
        ////////////////////////////////////////////////////////////////////////////
        var responseTypes = context.ResponseTypes;
        if (responseTypes.Count is 0)
        {
            logger.LogError(context, "Missing response_type");
            context.InvalidRequest(AuthorizeErrors.UnsupportedResponseType, "Missing response_type");
            return ValueTask.CompletedTask;
        }

        if (!context.Options.Discovery.ResponseTypesIsSupported(responseTypes))
        {
            logger.LogError(context, "Response type not supported", responseTypes.ToSpaceSeparatedString());
            context.InvalidRequest(AuthorizeErrors.UnsupportedResponseType, "Response type not supported");
            return ValueTask.CompletedTask;
        }

        if (!responseTypes.All(client.AllowedResponseTypes.Contains))
        {
            logger.LogError(
                context, 
                "Response type not allowed for the client",
                $"{responseTypes.ToSpaceSeparatedString()} - {client.Id} - {client.Name}");

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
            if (!context.Options.Discovery.ResponseModeIsSupported(responseMode))
            {
                logger.LogError(context, "Unsupported response_mode", responseMode);
                context.InvalidRequest(AuthorizeErrors.UnsupportedResponseMode);
                return ValueTask.CompletedTask;
            }

            // when a token is required, the response mode should be form_post
            if (responseMode != ResponseModes.FormPost && responseTypes.Any(t => t != ResponseTypes.Code))
            {
                logger.LogError(
                    context,
                    "Invalid response_mode for response_type",
                    $"{responseMode} - {responseTypes.ToSpaceSeparatedString()}");

                context.InvalidRequest(AuthorizeErrors.InvalidRequest, "Invalid response_mode for response_type");

                return ValueTask.CompletedTask;
            }            
        }
        else
        {
            // set default response_mode
            if (responseTypes.Contains(ResponseTypes.Token) || responseTypes.Contains(ResponseTypes.IdToken))
                context.ResponseMode = ResponseModes.FormPost;
            else
                context.ResponseMode = ResponseModes.Query;
        }


        //////////////////////////////////////////////////////////
        // scope must be present
        //////////////////////////////////////////////////////////
        if (context.Scope.IsMissing())
        {
            logger.LogError(context, "scope is missing");
            context.InvalidRequest(AuthorizeErrors.InvalidScope);
            return ValueTask.CompletedTask;
        }

        if (context.Scope.Length > options.InputLengthRestrictions.Scope)
        {
            logger.LogError(context, "Scopes too long");
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
                logger.LogError(context, "Nonce too long");
                context.InvalidRequest("Invalid nonce", "too long");
                return ValueTask.CompletedTask;
            }
        }
        else if (context.Resources.IsOpenId && 
            context.ResponseTypes.Contains(ResponseTypes.Token))
        {
            logger.LogError(context, "Nonce required for implicit flow with openid scope");
            context.InvalidRequest("Invalid nonce", "required");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check prompt
        //////////////////////////////////////////////////////////
        if (context.PromptModes.Count > 1 && context.PromptModes.Contains(PromptModes.None))
        {
            logger.LogError(context, "The property prompt contains 'none' and other values. 'none' should be used by itself.");
            context.InvalidRequest("Invalid prompt");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check ui locales
        //////////////////////////////////////////////////////////
        if (context.UiLocales.IsPresent() && context.UiLocales.Length > options.InputLengthRestrictions.UiLocale)
        {
            logger.LogError(context, "UI locale too long");
            context.InvalidRequest("Invalid ui_locales");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check login_hint
        //////////////////////////////////////////////////////////
        if (context.LoginHint.IsPresent() && context.LoginHint.Length > options.InputLengthRestrictions.LoginHint)
        {
            logger.LogError(context, "Login hint too long");
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
                logger.LogError(context, "Acr values too long");
                context.InvalidRequest("Invalid acr_values", "too long");
                return ValueTask.CompletedTask;
            }

            // TODO: check acr_values, valide against future realm options
        }

        return ValueTask.CompletedTask;
    }
}
