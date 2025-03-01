using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
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

            return new(EndpointErrorResults.MethodNotAllowed(httpContext));
        }

        if (!httpContext.Request.HasApplicationFormContentType())
        {
            logger.LogWarning("Invalid media type");

            return new(EndpointErrorResults.UnsupportedMediaType(httpContext));
        }

        var serverOptions = httpContext.GetCurrentRealm().Options.ServerOptions;
        var items = ContextItems.From(serverOptions);
        var parameters = httpContext.Request.Form.AsNameValueCollection();
        var context = new RevocationContext(httpContext, parameters, items);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}