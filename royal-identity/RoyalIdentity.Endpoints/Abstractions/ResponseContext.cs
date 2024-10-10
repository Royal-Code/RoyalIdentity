using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Defaults;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// Default implementation of a context for a direct response to the request.
/// </summary>
public sealed class ResponseContext : AbstractContextBase
{
    /// <summary>
    /// Create a new <see cref="ResponseContext"/>.
    /// </summary>
    /// <param name="httpContext">The HttpContext.</param>
    /// <param name="responseHandler">The <see cref="IResponseHandler"/> for the request.</param>
    public ResponseContext(HttpContext httpContext, IResponseHandler responseHandler) 
        : base(httpContext, null)
    {
        Response = responseHandler;
    }

    /// <summary>
    /// Create a new <see cref="ResponseContext"/>.
    /// </summary>
    /// <param name="httpContext">The HttpContext.</param>
    /// <param name="result">The <see cref="IResult"/> for the request.</param>
    public ResponseContext(HttpContext httpContext, IResult result)
        : base(httpContext, null)
    {
        Response = new ResponseHandler(result);
    }
}