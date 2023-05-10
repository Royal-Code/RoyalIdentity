
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoints.Abstractions;

public abstract class AbstractContextBase : IContextBase
{
    protected AbstractContextBase(
        HttpContext httpContext,
        ContextItems? items = null)
    {
        HttpContext = httpContext;
        Items = items ?? new ContextItems();
    }

    public HttpContext HttpContext { get; }

    public ContextItems Items { get; }

    public IResponseHandler? Response { get; set; }
    
}