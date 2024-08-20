using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// Default and abstract implementation of <see cref="IContextBase"/>.
/// </summary>
public abstract class AbstractContextBase : IContextBase
{
    /// <summary>
    /// Receives the necessary dependencies.
    /// </summary>
    /// <param name="httpContext">AspNetCore Http context.</param>
    /// <param name="items">Optional, the context items.</param>
    protected AbstractContextBase(
        HttpContext httpContext,
        ContextItems? items = null)
    {
        HttpContext = httpContext;
        Items = items ?? new();
    }

    /// <inheritdoc/>
    public HttpContext HttpContext { get; }

    /// <inheritdoc/>
    public ContextItems Items { get; }

    /// <inheritdoc/>
    public IResponseHandler? Response { get; set; }
    
}