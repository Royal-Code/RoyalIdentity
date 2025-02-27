using Microsoft.AspNetCore.Http;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using System.Collections.Specialized;

namespace RoyalIdentity.Contexts;

/// <summary>
/// Base context class for all endpoints.
/// </summary>
public abstract class EndpointContextBase : AbstractContextBase, IEndpointContextBase
{
    protected EndpointContextBase(HttpContext httpContext, NameValueCollection raw, ContextItems? items = null)
        : base(httpContext, items)
    {
        Raw = raw;
        Realm = httpContext.GetCurrentRealm();
    }

    /// <summary>
    /// Gets the original values of the request.
    /// </summary>
    public NameValueCollection Raw { get; }

    /// <inheritdoc />
    public Realm Realm { get; }

    /// <inheritdoc />
    public RealmOptions Options => Realm.Options;

    /// <inheritdoc />
    public ServerOptions ServerOptions => Options.ServerOptions;
}
