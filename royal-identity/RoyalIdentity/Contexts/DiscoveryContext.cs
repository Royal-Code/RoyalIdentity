using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contexts;

public class DiscoveryContext : AbstractContextBase
{
    public DiscoveryContext(
        HttpContext httpContext,
        ServerOptions options,
        ContextItems? items = null) : base(httpContext, items)
    {
        Options = options;
        BaseUrl = httpContext.GetServerBaseUrl().EnsureTrailingSlash();
        IssuerUri = httpContext.GetServerIssuerUri(options);
    }

    public ServerOptions Options { get; }

    public string BaseUrl { get; }

    public string IssuerUri { get; }
}