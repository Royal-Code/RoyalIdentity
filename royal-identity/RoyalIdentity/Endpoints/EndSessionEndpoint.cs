using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using System.Collections.Specialized;
using RoyalIdentity.Contexts;

namespace RoyalIdentity.Endpoints;

public class EndSessionEndpoint : IEndpointHandler
{
    private readonly ILogger logger;

    public EndSessionEndpoint(ILogger<EndSessionEndpoint> logger)
    {
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

        var serverOptions = httpContext.GetCurrentRealm().Options.ServerOptions;
        var items = ContextItems.From(serverOptions);
        var context = new EndSessionContext(httpContext, parameters, httpContext.User, items);

        return new EndpointCreationResult(context);
    }
}
