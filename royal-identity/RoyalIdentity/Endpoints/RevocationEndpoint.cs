using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Endpoints;

public class RevocationEndpoint : IEndpointHandler
{
    private readonly ILogger logger;
    private readonly ServerOptions options;

    public RevocationEndpoint(
        ILogger<RevocationEndpoint> logger,
        IOptions<ServerOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing revocation request.");

        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            logger.LogWarning("Invalid HTTP method");

            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status405MethodNotAllowed,
                Title = "Method Not Allowed",
                Detail = "Invalid HTTP request for token endpoint"
            };

            return ValueTask.FromResult(
                new EndpointCreationResult(
                    httpContext,
                    ResponseHandler.Problem(problemDetails)));
        }

        if (!httpContext.Request.HasApplicationFormContentType())
        {
            logger.LogWarning("Invalid media type");

            // return a problem details of a UnsupportedMediaType informing the ContentType is invalid
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status415UnsupportedMediaType,
                Title = "Invalid Content Type",
                Detail = "The content type must be: application/x-www-form-urlencoded"
            };

            return ValueTask.FromResult(
                new EndpointCreationResult(
                    httpContext,
                    ResponseHandler.Problem(problemDetails)));
        }

        var items = ContextItems.From(options);
        var parameters = httpContext.Request.Form.AsNameValueCollection();
        var context = new RevocationContext(httpContext, parameters, items);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}