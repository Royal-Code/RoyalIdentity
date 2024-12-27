using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Responses.HttpResults;

public class TokenResult : IResult
{
    private readonly TokenEndpointParameters values;

    public TokenResult(TokenEndpointParameters values)
    {
        this.values = values;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.SetNoCache();
        return httpContext.Response.WriteJsonAsync(values);
    }
}
