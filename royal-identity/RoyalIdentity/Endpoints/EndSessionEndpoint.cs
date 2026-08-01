// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Pipelines.Abstractions;
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
                return EndpointErrors.UnsupportedMediaType(httpContext);
            }

            parameters = (await httpContext.Request.ReadFormAsync()).AsNameValueCollection();
        }
        else
        {
            logger.LogWarning("Invalid HTTP method for end session endpoint.");
            return EndpointErrors.MethodNotAllowed(httpContext, HttpMethods.Get, HttpMethods.Post);
        }

        var items = new ContextItems();
        var context = new EndSessionContext(httpContext, parameters, httpContext.User, items);

        return new EndpointCreationResult(context);
    }
}
