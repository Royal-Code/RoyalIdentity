
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// Base interface for all contexts
/// </summary>
public interface IContextBase
{
    HttpContext HttpContext { get; }

    ContextItems Items { get; }

    IResponseHandler? Response { get; }
}

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