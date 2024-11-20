using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Endpoints.Defaults;
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
            {
                // return a problem details of a UnsupportedMediaType informing the ContentType is invalid
                var problemDetails = new ProblemDetails
                {
                    Type = "about:blank",
                    Status = StatusCodes.Status415UnsupportedMediaType,
                    Title = "Invalid ContentType",
                    Detail = "The content type must be: application/x-www-form-urlencoded"
                };

                return ValueTask.FromResult(
                    new EndpointCreationResult(
                        httpContext,
                        ResponseHandler.Problem(problemDetails)));
            }

            values = httpContext.Request.Form.AsNameValueCollection();
        }
        else
        {
            // return a problem details of a MethodNotAllowed infoming the http method is not allowed
            return new(EndpointProblemResults.MethodNotAllowed(httpContext));
        }

        var items = ContextItems.From(options);
        var context = new AuthorizeContext(httpContext, values, httpContext.User, items);

        context.Load(logger);

        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}