using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using System.Collections.Specialized;

namespace RoyalIdentity.Endpoints;

/// <summary>
/// Manipulates the 'authorize' endpoint specified by 'oauth', generating the context according to the input.
/// </summary>
public class AuthorizeEndpoint : IEndpointHandler
{
    private readonly ServerOptions options;
    private readonly ILogger logger;

    public AuthorizeEndpoint(IOptions<ServerOptions> options, ILogger<AuthorizeEndpoint> logger)
    {
        this.options = options.Value;
        this.logger = logger;
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Start authorize enpoint");

        NameValueCollection values;

        if (HttpMethods.IsGet(httpContext.Request.Method))
        {
            values = httpContext.Request.Query.AsNameValueCollection();
        }
        else if (HttpMethods.IsPost(httpContext.Request.Method))
        {
            if (!httpContext.Request.HasApplicationFormContentType())
                return ValueTask.FromResult(EndpointErrorResults.UnsupportedMediaType(httpContext));

            values = httpContext.Request.Form.AsNameValueCollection();
        }
        else
        {
            // return a problem details of a MethodNotAllowed infoming the http method is not allowed
            return new(EndpointErrorResults.MethodNotAllowed(httpContext));
        }

        var items = ContextItems.From(options);
        var context = new AuthorizeContext(httpContext, values, httpContext.User, items);

        context.Load(logger);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}