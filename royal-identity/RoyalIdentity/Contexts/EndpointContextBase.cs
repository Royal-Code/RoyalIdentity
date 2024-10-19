using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using System.Collections.Specialized;

namespace RoyalIdentity.Contexts;

public abstract class EndpointContextBase : AbstractContextBase, IEndpointContextBase
{
    protected EndpointContextBase(HttpContext httpContext, NameValueCollection raw, ContextItems? items = null)
        : base(httpContext, items)
    {
        Raw = raw;
    }

    /// <summary>
    /// Gets the original values of the request.
    /// </summary>
    public NameValueCollection Raw { get; }

    /// <summary>
    /// Gets or sets the UI locales.
    /// </summary>
    /// <value>
    /// The UI locales.
    /// </value>
    public string? UiLocales { get; set; }
}
