using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using System.Collections.Specialized;
using System.Security.Claims;

namespace RoyalIdentity.Contexts;

public class AuthorizeValidateContext : AuthorizeContext
{
    public AuthorizeValidateContext(
        HttpContext httpContext,
        NameValueCollection raw,
        ClaimsPrincipal? subject = null,
        ContextItems? items = null) : base(httpContext, raw, subject, items)
    { }
}
