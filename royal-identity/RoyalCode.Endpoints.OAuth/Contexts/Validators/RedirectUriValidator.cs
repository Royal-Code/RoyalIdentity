using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts;
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
        IOptions<ServerOptions> options, 
        IRedirectUriValidator uriValidator,
        ILogger<RedirectUriValidator> logger) 
    {
        this.options = options.Value;
        this.uriValidator = uriValidator;
        this.logger = logger;
    }

    public async ValueTask Validate(IWithRedirectUri context, CancellationToken cancellationToken)
    {
        context.AssertHasClient();

        if (context.RedirectUri.IsMissingOrTooLong(options.InputLengthRestrictions.RedirectUri))
        {
            logger.LogError(options, "The parameter redirect_uri is missing or too long", context);
            context.InvalidRequest("Invalid redirect_uri");

            return;
        }
        else if (!Uri.TryCreate(context.RedirectUri, UriKind.Absolute, out _))
        {
            logger.LogError(options, "Malformed redirect_uri", context.RedirectUri, context);
            context.InvalidRequest("Invalid redirect_uri", context.RedirectUri);

            return;
        }

        //////////////////////////////////////////////////////////
        // check if client protocol type is oidc
        //////////////////////////////////////////////////////////
        if (context.Client.ProtocolType is not ServerConstants.ProtocolTypes.OpenIdConnect)
        {
            logger.LogError(options, "Invalid protocol type for OIDC authorize endpoint", context.Client.ProtocolType, context);
            context.InvalidRequest(OidcConstants.AuthorizeErrors.UnauthorizedClient, "Invalid protocol");

            return;
        }

        //////////////////////////////////////////////////////////
        // check if redirect_uri is valid
        //////////////////////////////////////////////////////////
        if (!await uriValidator.IsRedirectUriValidAsync(context.RedirectUri, context.Client))
        {
            logger.LogError(options, "Invalid redirect_uri", context.RedirectUri, context);
            context.InvalidRequest(OidcConstants.AuthorizeErrors.InvalidRequest, "Invalid redirect_uri");

            return;
        }

        context.Items.GetOrCreate<Asserts>().HasRedirectUri = true;
    }
}
