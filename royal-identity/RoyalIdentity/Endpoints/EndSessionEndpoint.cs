using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using System.Collections.Specialized;
using RoyalIdentity.Contexts;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Endpoints;

public class EndSessionEndpoint : IEndpointHandler
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public EndSessionEndpoint(IOptions<ServerOptions> options, ILogger<EndSessionEndpoint> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public async ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Processing End Session request.");

        NameValueCollection parameters;
        if (HttpMethods.IsGet(httpContext.Request.Method))
        {
            parameters = httpContext.Request.Query.AsNameValueCollection();
        }
        else if (HttpMethods.IsPost(httpContext.Request.Method))
        {
            if (!httpContext.Request.HasApplicationFormContentType())
            {
                // return a problem details of a UnsupportedMediaType informing the ContentType is invalid
                var problemDetails = new ProblemDetails
                {
                    Type = "about:blank",
                    Status = StatusCodes.Status415UnsupportedMediaType,
                    Title = "Invalid ContentType",
                    Detail = "The content type must be: application/x-www-form-urlencoded"
                };

                return new EndpointCreationResult(
                        httpContext,
                        ResponseHandler.Problem(problemDetails));
            }

            parameters = (await httpContext.Request.ReadFormAsync()).AsNameValueCollection();
        }
        else
        {
            logger.LogWarning("Invalid HTTP method for end session endpoint.");
            var problemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Status = StatusCodes.Status405MethodNotAllowed,
                Title = TokenErrors.InvalidRequest,
                Detail = "Method not allowed for end session endpoint"
            };

            return new EndpointCreationResult(
                    httpContext,
                    ResponseHandler.Problem(problemDetails));
        }

        var items = ContextItems.From(options);
        var context = new EndSessionContext(httpContext, parameters, httpContext.User, items);

        return new EndpointCreationResult(context);
    }
}
