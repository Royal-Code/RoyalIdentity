using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contexts;

public class JwkContext : AbstractContextBase
{
    public JwkContext(HttpContext httpContext, ServerOptions options, ContextItems? items = null) 
        : base(httpContext, items) 
    {
        Options = options;
    }

    public ServerOptions Options { get; }
}
