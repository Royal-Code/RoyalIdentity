using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
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
        logger.LogTrace("Processing token request.");

        // validate HTTP
        if (!HttpMethods.IsPost(httpContext.Request.Method) || !httpContext.Request.HasApplicationFormContentType())
        {
            logger.LogWarning("Invalid HTTP request for token endpoint");

            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = TokenErrors.InvalidRequest,
                Detail = "Invalid HTTP request for token endpoint"
            };

            return new EndpointCreationResult(
                httpContext,
                ResponseHandler.Problem(problemDetails));
        }

        // validate request
        var form = await httpContext.Request.ReadFormAsync();
        var parameters = form.AsNameValueCollection();

        if (!parameters.TryGet(TokenRequest.GrantType, out var grantType))
        {
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = TokenErrors.InvalidGrant,
                Detail = "Grant type not found"
            };

            return new EndpointCreationResult(
                httpContext,
                ResponseHandler.Problem(problemDetails));
        }

        if (grantType.Length > options.InputLengthRestrictions.GrantType)
        {
            logger.LogError("Grant type is too long");

            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = TokenErrors.UnsupportedGrantType,
                Detail = "Grant type is too long"
            };

            return new EndpointCreationResult(
                httpContext,
                ResponseHandler.Problem(problemDetails));
        }

        var items = ContextItems.From(options);
        ITokenEndpointContextBase? context = null;
        switch (grantType)
        {
            case GrantTypes.AuthorizationCode:
                context = new AuthorizationCodeContext(httpContext, parameters, grantType, items);
                break;
            case GrantTypes.RefreshToken:

                break;
            case GrantTypes.ClientCredentials:

                break;
            case GrantTypes.DeviceCode:

                break;
            default:
                if (extensionsGrantsProvider.GetAvailableGrantTypes().Contains(grantType))
                {
                    // Executar grant_type
                }
                break;
        }

        if (context is null)
        {
            logger.LogError("Grant type not supported: {GrantType}", grantType);

            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status400BadRequest,
                Title = TokenErrors.UnsupportedGrantType,
                Detail = "Grant type not supported"
            };

            return new EndpointCreationResult(
                httpContext,
                ResponseHandler.Problem(problemDetails));
        }

        context.Load(logger);

        return new EndpointCreationResult(context);
    }
}
