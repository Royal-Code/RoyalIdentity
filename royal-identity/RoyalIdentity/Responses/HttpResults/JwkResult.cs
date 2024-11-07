using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Responses.HttpResults;

public class JwkResult : IResult, IStatusCodeHttpResult
{
    private readonly IReadOnlyList<JsonWebKey> jwks;
    private readonly int? maxAge;

    public JwkResult(IReadOnlyList<JsonWebKey> jwks, int? maxAge)
    {
        this.jwks = jwks;
        this.maxAge = maxAge;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        if (maxAge.HasValue && maxAge.Value >= 0)
        {
            httpContext.Response.SetCache(maxAge.Value, "Origin");
        }

        return httpContext.Response.WriteJsonAsync(new { keys = jwks }, "application/json; charset=UTF-8");
    }

    public int? StatusCode => 200;
}