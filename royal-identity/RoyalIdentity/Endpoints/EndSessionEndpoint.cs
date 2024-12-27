using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using System.Collections.Specialized;
using RoyalIdentity.Contexts;

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
                logger.LogWarning("Unsupported media type, content type is not valid.");

                // return a problem details of a UnsupportedMediaType informing the ContentType is invalid
                return EndpointErrorResults.UnsupportedMediaType(httpContext);
            }

            parameters = (await httpContext.Request.ReadFormAsync()).AsNameValueCollection();
        }
        else
        {
            logger.LogWarning("Invalid HTTP method for end session endpoint.");
            return EndpointErrorResults.MethodNotAllowed(httpContext);
        }

        var items = ContextItems.From(options);
        var context = new EndSessionContext(httpContext, parameters, httpContext.User, items);

        return new EndpointCreationResult(context);
    }
}
