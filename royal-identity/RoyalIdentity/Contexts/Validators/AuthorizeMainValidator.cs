using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Contexts.Validators;

public class AuthorizeMainValidator : IValidator<IAuthorizationContextBase>
{
    private readonly ILogger logger;
    private readonly ISessionStateGenerator sessionStateGenerator;

    public AuthorizeMainValidator(
        ISessionStateGenerator sessionStateGenerator,
        ILogger<AuthorizeMainValidator> logger)
    {
        this.sessionStateGenerator = sessionStateGenerator;
        this.logger = logger;
    }

    public ValueTask Validate(IAuthorizationContextBase context, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClient();
        var client = context.ClientParameters.Client;
        var restrictions = context.Options.InputLengthRestrictions;

        ////////////////////////////////////////////////////////////////////////////
        // response_type must be present and supported and allowed for the client
        ////////////////////////////////////////////////////////////////////////////
        var responseTypes = context.ResponseTypes;
        if (responseTypes.Count is 0)
        {
            logger.LogError(context, "Missing response_type");
            context.Error(Oidc.Authorize.Errors.UnsupportedResponseType, "Missing response_type");
            return ValueTask.CompletedTask;
        }

        if (responseTypes.Count is not 1 || !responseTypes.Contains(Oidc.ResponseTypes.Code))
        {
            logger.LogError(context, "Response type not supported", responseTypes.ToSpaceSeparatedString());
            context.Error(Oidc.Authorize.Errors.UnsupportedResponseType, "Response type not supported");
            return ValueTask.CompletedTask;
        }

        if (!responseTypes.All(client.AllowedResponseTypes.Contains))
        {
            logger.LogError(
                context, 
                "Response type not allowed for the client",
                $"{responseTypes.ToSpaceSeparatedString()} - {client.Id} - {client.Name}");

            context.Error(Oidc.Authorize.Errors.UnsupportedResponseType, "Response type not allowed");
            
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check response_mode parameter and set response_mode
        //////////////////////////////////////////////////////////

        // check if response_mode parameter is present and valid
        var responseMode = context.ResponseMode;
        if (responseMode.IsPresent())
        {
            if (!IsSupportedResponseMode(responseMode))
            {
                logger.LogError(context, "Unsupported response_mode", responseMode);
                context.Error(Oidc.Authorize.Errors.UnsupportedResponseMode, "Response mode not supported");
                return ValueTask.CompletedTask;
            }

        }
        else
        {
            context.ResponseMode = Oidc.ResponseModes.Query;
        }


        //////////////////////////////////////////////////////////
        // scope must be present
        //////////////////////////////////////////////////////////
        if (context.Scope.IsMissing())
        {
            logger.LogError(context, "scope is missing");
            context.Error(Oidc.Authorize.Errors.InvalidScope, "scope is missing");
            return ValueTask.CompletedTask;
        }

        if (context.Scope.Length > restrictions.Scope)
        {
            logger.LogError(context, "Scopes too long");
            context.Error(Oidc.Authorize.Errors.InvalidScope, "scopes too long");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check nonce
        //////////////////////////////////////////////////////////
        if (context.Nonce.IsPresent())
        {
            if (context.Nonce.Length > restrictions.Nonce)
            {
                logger.LogError(context, "Nonce too long");
                context.Error(Oidc.Authorize.Errors.InvalidRequest, "Invalid nonce: too long");
                return ValueTask.CompletedTask;
            }
        }
        //////////////////////////////////////////////////////////
        // check prompt
        //////////////////////////////////////////////////////////
        if (context.RequestedPromptModes.Count > 1 &&
            context.RequestedPromptModes.Contains(Oidc.PromptModes.None))
        {
            logger.LogError(context, "The property prompt contains 'none' and other values. 'none' should be used by itself.");

            // AuthorizeValidateContext is the server-side continuation validator. Its caller consumes a
            // ProblemDetails contract, not an Authentication Response. The browser-facing authorize request,
            // whose client and redirect URI have already been validated at this point, uses the bounded factory.
            if (context is not AuthorizeContext browserContext || context is AuthorizeValidateContext)
            {
                context.Error(Oidc.Authorize.Errors.InvalidRequest, "Invalid prompt");
            }
            else
            {
                context.Response = AuthorizeResponseFactory.CreateError(
                    sessionStateGenerator,
                    browserContext,
                    Oidc.Authorize.Errors.InvalidRequest,
                    "The prompt value 'none' must not be combined with another value.");
            }

            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check ui locales
        //////////////////////////////////////////////////////////
        if (context.UiLocales.IsPresent() && context.UiLocales.Length > restrictions.UiLocale)
        {
            logger.LogError(context, "UI locale too long");
            context.Error(Oidc.Authorize.Errors.InvalidRequest, "Invalid ui_locales");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check login_hint
        //////////////////////////////////////////////////////////
        if (context.LoginHint.IsPresent() && context.LoginHint.Length > restrictions.LoginHint)
        {
            logger.LogError(context, "Login hint too long");
            context.Error(Oidc.Authorize.Errors.InvalidRequest, "Invalid login_hint: too long");
            return ValueTask.CompletedTask;
        }


        //////////////////////////////////////////////////////////
        // check acr_values
        //////////////////////////////////////////////////////////
        var acrValues = context.Raw.Get(Oidc.Authorize.Request.AcrValues);
        if (acrValues is not null && acrValues.Length > restrictions.AcrValues)
        {
            logger.LogError(context, "Acr values too long");
            context.Error(Oidc.Authorize.Errors.InvalidRequest, "Invalid acr_values: too long");
            return ValueTask.CompletedTask;
        }

        return ValueTask.CompletedTask;
    }

    private static bool IsSupportedResponseMode(string responseMode)
        => responseMode is Oidc.ResponseModes.Query
            or Oidc.ResponseModes.Fragment
            or Oidc.ResponseModes.FormPost;
}
