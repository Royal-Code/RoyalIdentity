using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Extensions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Endpoints;

public class TokenEndpoint : IEndpointHandler
{
    private readonly ILogger _logger;
    private readonly IExtensionsGrantsProvider extensionsGrantsProvider;



    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        _logger.LogTrace("Processing token request.");

        // validate HTTP
        if (!HttpMethods.IsPost(httpContext.Request.Method) || !httpContext.Request.HasApplicationFormContentType())
        {
            _logger.LogWarning("Invalid HTTP request for token endpoint");

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

        switch (grantType)
        {
            case GrantTypes.AuthorizationCode:

                break;

            case GrantTypes.RefreshToken:

                break;

            case GrantTypes.ClientCredentials:

                break;

            case GrantTypes.DeviceCode:

            default:

                if (extensionsGrantsProvider.GetAvailableGrantTypes().Contains(grantType))
                {
                    // Executar grant_type
                }

                break;
        }


        throw new NotImplementedException("");
    }
}
