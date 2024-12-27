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

            return new(EndpointErrorResults.MethodNotAllowed(httpContext));
        }

        if (!httpContext.Request.HasApplicationFormContentType())
        {
            logger.LogWarning("Invalid media type");

            return new(EndpointErrorResults.UnsupportedMediaType(httpContext));
        }

        var items = ContextItems.From(options);
        var parameters = httpContext.Request.Form.AsNameValueCollection();
        var context = new RevocationContext(httpContext, parameters, items);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}