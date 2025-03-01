using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Validators;

internal class RedirectUriValidator : IValidator<IWithRedirectUri>
{
    private readonly ServerOptions options;
    private readonly IRedirectUriValidator uriValidator;
    private readonly ILogger logger;

    public RedirectUriValidator(
        IStorage storage,
        IRedirectUriValidator uriValidator,
        ILogger<RedirectUriValidator> logger) 
    {
        options = storage.ServerOptions;
        this.uriValidator = uriValidator;
        this.logger = logger;
    }

    public async ValueTask Validate(IWithRedirectUri context, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClient();
        var client = context.ClientParameters.Client;

        if (context.RedirectUri.IsMissingOrTooLong(options.InputLengthRestrictions.RedirectUri))
        {
            logger.LogError(context, "The parameter redirect_uri is missing or too long");
            context.InvalidRequest("Invalid redirect_uri");

            return;
        }
        else if (!Uri.TryCreate(context.RedirectUri, UriKind.Absolute, out _))
        {
            logger.LogError(context, "Malformed redirect_uri", context.RedirectUri);
            context.InvalidRequest("Invalid redirect_uri", context.RedirectUri);

            return;
        }

        //////////////////////////////////////////////////////////
        // check if client protocol type is oidc
        //////////////////////////////////////////////////////////
        if (client.ProtocolType is not ServerConstants.ProtocolTypes.OpenIdConnect)
        {
            logger.LogError(context, "Invalid protocol type for OIDC authorize endpoint", client.ProtocolType);
            context.InvalidRequest(OidcConstants.AuthorizeErrors.UnauthorizedClient, "Invalid protocol");

            return;
        }

        //////////////////////////////////////////////////////////
        // check if redirect_uri is valid
        //////////////////////////////////////////////////////////
        if (!await uriValidator.IsRedirectUriValidAsync(context.RedirectUri, client))
        {
            logger.LogError(context, "Invalid redirect_uri", context.RedirectUri);
            context.InvalidRequest(OidcConstants.AuthorizeErrors.InvalidRequest, "Invalid redirect_uri");

            return;
        }

        context.RedirectUriValidated();
    }
}
