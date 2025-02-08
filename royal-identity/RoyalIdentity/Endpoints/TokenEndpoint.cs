using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Endpoints;

public class TokenEndpoint : IEndpointHandler
{
    private readonly ServerOptions options;
    private readonly IExtensionsGrantsProvider extensionsGrantsProvider;
    private readonly ILogger logger;

    public TokenEndpoint(
        IOptions<ServerOptions> options,
        IExtensionsGrantsProvider extensionsGrantsProvider,
        ILogger<TokenEndpoint> logger)
    {
        this.options = options.Value;
        this.extensionsGrantsProvider = extensionsGrantsProvider;
        this.logger = logger;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing token request.");

        // validate HTTP method
        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            logger.LogWarning("Invalid HTTP request for token endpoint, invalid method");

            return EndpointErrorResults.MethodNotAllowed(httpContext);
        }

        // validate HTTP content type
        if (!httpContext.Request.HasApplicationFormContentType())
        {
            logger.LogWarning("Invalid HTTP request for token endpoint, content type");

            return EndpointErrorResults.UnsupportedMediaType(httpContext);
        }

        // read parameters
        var form = await httpContext.Request.ReadFormAsync();
        var parameters = form.AsNameValueCollection();

        // validate request
        if (!parameters.TryGet(TokenRequest.GrantType, out var grantType))
        {
            logger.LogWarning("Grant type parameter not found");

            return EndpointErrorResults.InvalidRequest(httpContext, "Grant type parameter not found");
        }

        if (grantType.Length > options.InputLengthRestrictions.GrantType)
        {
            logger.LogError("Grant type is too long");

            return EndpointErrorResults.InvalidRequest(httpContext, "Grant type is too long");
        }

        // create the context
        var items = ContextItems.From(options);
        ITokenEndpointContextBase? context = null;
        switch (grantType)
        {
            case GrantTypes.AuthorizationCode:
                context = new AuthorizationCodeContext(httpContext, parameters, items);
                break;
            case GrantTypes.RefreshToken:
                context = new RefreshTokenContext(httpContext, parameters, items);
                break;
            case GrantTypes.ClientCredentials:
                context = new ClientCredentialsContext(httpContext, parameters, items);
                break;
            case GrantTypes.DeviceCode:

                break;
            case GrantTypes.TokenExchange:

                break;
            default:
                if (extensionsGrantsProvider.GetAvailableGrantTypes().Contains(grantType))
                {
                    context = await extensionsGrantsProvider.CreateContextAsync(grantType, httpContext.RequestAborted);
                }
                break;
        }

        // validate if grant type is supported
        if (context is null)
        {
            logger.LogError("Grant type not supported: {GrantType}", grantType);

            return EndpointErrorResults.BadRequest(httpContext, TokenErrors.UnsupportedGrantType, "Grant type not supported");
        }

        context.Load(logger);

        return new EndpointCreationResult(context);
    }
}
