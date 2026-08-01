using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Endpoints;

public class RevocationEndpoint : IEndpointHandler
{
    private readonly ILogger logger;

    public RevocationEndpoint(ILogger<RevocationEndpoint> logger)
    {
        this.logger = logger;
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing revocation request.");

        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            logger.LogWarning("Invalid HTTP method");

            return new(EndpointErrors.MethodNotAllowed(httpContext, HttpMethods.Post));
        }

        if (!httpContext.Request.HasApplicationFormContentType())
        {
            logger.LogWarning("Invalid media type");

            return new(EndpointErrors.UnsupportedMediaType(httpContext));
        }

        var parameters = httpContext.Request.Form.AsNameValueCollection();

        if (!DirectRequestPreflight.TryEvaluate(
                httpContext,
                parameters,
                DirectRequestPreflight.RevocationRequestParameters,
                logger,
                out var clientAuthentication,
                out var preflightFailure))
        {
            return ValueTask.FromResult(preflightFailure);
        }

        var items = ContextItems.From(clientAuthentication);
        var context = new RevocationContext(httpContext, parameters, items);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}
