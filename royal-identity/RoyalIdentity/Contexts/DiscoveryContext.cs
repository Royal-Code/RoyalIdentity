using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contexts;

public class DiscoveryContext : EndpointContextBase
{
    public DiscoveryContext(
        HttpContext httpContext,
        RealmOptions options,
        ContextItems? items = null) : base(httpContext, [], items)
    {
        BaseUrl = httpContext.GetServerBaseUrl().EnsureTrailingSlash();
        IssuerUri = httpContext.GetServerIssuerUri(options);
    }

    public string BaseUrl { get; }

    public string IssuerUri { get; }
}