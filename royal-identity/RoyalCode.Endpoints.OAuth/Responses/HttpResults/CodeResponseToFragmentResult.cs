using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;
using System.Collections.Specialized;

namespace RoyalIdentity.Responses.HttpResults;

public class CodeResponseToFragmentResult : IResult, IStatusCodeHttpResult
{
    private readonly string redirectUri;
    private readonly NameValueCollection parameters;

    public CodeResponseToFragmentResult(string redirectUri, NameValueCollection parameters)
    {
        this.redirectUri = redirectUri;
        this.parameters = parameters;
    }

    public int? StatusCode => 200;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.SetNoCache();
        httpContext.Response.Redirect(BuildRedirectUri());
        return Task.CompletedTask;
    }

    private string BuildRedirectUri()
    {
        return redirectUri.AddHashFragment(parameters.ToQueryString());
    }
}