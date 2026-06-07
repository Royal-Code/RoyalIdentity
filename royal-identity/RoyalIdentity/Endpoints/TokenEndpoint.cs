using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Endpoints;

public class TokenEndpoint : IEndpointHandler
{
    private readonly IExtensionsGrantsProvider extensionsGrantsProvider;
    private readonly ILogger logger;

    public TokenEndpoint(
        IExtensionsGrantsProvider extensionsGrantsProvider,
        ILogger<TokenEndpoint> logger)
    {
        this.extensionsGrantsProvider = extensionsGrantsProvider;
        this.logger = logger;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing token request.");

        var realmOptions = httpContext.GetRealmOptions();

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
        if (!parameters.TryGet(Oidc.Token.Request.GrantType, out var grantType))
        {
            logger.LogWarning("Grant type parameter not found");

            return EndpointErrorResults.InvalidRequest(httpContext, "Grant type parameter not found");
        }

        if (grantType.Length > realmOptions.InputLengthRestrictions.GrantType)
        {
            logger.LogError("Grant type is too long");

            return EndpointErrorResults.InvalidRequest(httpContext, "Grant type is too long");
        }

        // create the context
        var items = new ContextItems();
        ITokenEndpointContextBase? context = null;
        switch (grantType)
        {
            case OpenIdConnectGrantTypes.AuthorizationCode:
                context = new AuthorizationCodeContext(httpContext, parameters, items);
                break;
            case OpenIdConnectGrantTypes.RefreshToken:
                context = new RefreshTokenContext(httpContext, parameters, items);
                break;
            case OpenIdConnectGrantTypes.ClientCredentials:
                context = new ClientCredentialsContext(httpContext, parameters, items);
                break;
            case OpenIdConnectGrantTypes.DeviceCode:

                break;
            case OpenIdConnectGrantTypes.TokenExchange:

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

            return EndpointErrorResults.BadRequest(httpContext, Oidc.Token.Errors.UnsupportedGrantType, "Grant type not supported");
        }

        context.Load(logger);

        return new EndpointCreationResult(context);
    }
}
