using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contexts.Parameters;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Contexts;

public class UserInfoContext : EndpointContextBase, IWithBearerToken
{
    public UserInfoContext(HttpContext httpContext, ContextItems items, string token)
        : base(httpContext, new(), items)
    {
        Token = token;
    }

    public string Token { get; }

    public BearerParameters BearerParameters { get; } = new();
}
