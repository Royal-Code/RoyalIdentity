using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using System.Collections.Specialized;

namespace RoyalIdentity.Endpoints;

/// <summary>
/// Manipulates the 'authorize' endpoint specified by 'OAuth2', generating the context according to the input.
/// </summary>
public class AuthorizeEndpoint : IEndpointHandler
{
    private readonly ILogger logger;

    public AuthorizeEndpoint(ILogger<AuthorizeEndpoint> logger)
    {
        this.logger = logger;
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        logger.LogDebug("Start authorize endpoint");

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
            // return a problem details of a MethodNotAllowed informing the http method is not allowed
            return new(EndpointErrorResults.MethodNotAllowed(httpContext));
        }

        var serverOptions = httpContext.GetCurrentRealm().Options.ServerOptions;
        var items = ContextItems.From(serverOptions);
        var context = new AuthorizeContext(httpContext, values, httpContext.User, items);

        context.Load(logger);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}