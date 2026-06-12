using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using System.Collections.Specialized;

namespace RoyalIdentity.Contexts;

public class ProtectedResourceMetadataContext : EndpointContextBase
{
    public ProtectedResourceMetadataContext(
        HttpContext httpContext,
        NameValueCollection raw,
        RealmOptions options,
        string resourceUri,
        ContextItems? items = null) : base(httpContext, raw, items)
    {
        ResourceUri = resourceUri;
        BaseUrl = httpContext.GetServerBaseUrl().EnsureTrailingSlash();
        IssuerUri = httpContext.GetServerIssuerUri(options);
    }

    public string ResourceUri { get; }

    public string BaseUrl { get; }

    public string IssuerUri { get; }
}
