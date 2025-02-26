using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contexts;

public class DiscoveryContext : AbstractContextBase
{
    public DiscoveryContext(
        HttpContext httpContext,
        RealmOptions options,
        ContextItems? items = null) : base(httpContext, items)
    {
        Options = options;
        BaseUrl = httpContext.GetRealmBaseUrl().EnsureTrailingSlash();
        IssuerUri = httpContext.GetServerIssuerUri(options);
    }

    public RealmOptions Options { get; }

    public string BaseUrl { get; }

    public string IssuerUri { get; }
}