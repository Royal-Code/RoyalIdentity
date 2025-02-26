// Ignore Spelling: Jwk

using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contexts;

public class JwkContext : AbstractContextBase
{
    public JwkContext(HttpContext httpContext, RealmOptions options, ContextItems? items = null) 
        : base(httpContext, items) 
    {
        Options = options;
    }

    public RealmOptions Options { get; }
}
