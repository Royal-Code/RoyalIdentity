using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Responses.HttpResults;

/// <summary>
/// A failure of the HTTP layer, answered as <c>application/problem+json</c>.
/// </summary>
/// <remarks>
/// These failures happen before the request qualifies as a protocol request — a method the endpoint does not
/// serve, a body it cannot parse, an endpoint that is switched off. They are deliberately not shaped like an
/// OAuth error response: a payload with an <c>error</c> field would claim membership in the RFC 6749 §5.2
/// taxonomy that these conditions do not belong to, which is why the invented codes
/// (<c>method_not_allowed</c>, <c>invalid_content_type</c>, <c>not_found</c>) are gone (DF12).
/// </remarks>
public sealed class HttpFailureResult : IResult, IStatusCodeHttpResult
{
    private readonly string title;
    private readonly string? detail;
    private readonly IReadOnlyDictionary<string, string>? headers;

    public HttpFailureResult(
        int statusCode,
        string title,
        string? detail = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        StatusCode = statusCode;
        this.title = title;
        this.detail = detail;
        this.headers = headers;
    }

    public int? StatusCode { get; }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        if (headers is not null)
        {
            foreach (var header in headers)
                httpContext.Response.Headers[header.Key] = header.Value;
        }

        await Results.Problem(detail: detail, statusCode: StatusCode, title: title)
            .ExecuteAsync(httpContext);
    }
}
