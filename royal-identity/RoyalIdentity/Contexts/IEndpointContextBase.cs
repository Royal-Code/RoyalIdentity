using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using System.Collections.Specialized;

namespace RoyalIdentity.Contexts;

public interface IEndpointContextBase : IContextBase
{
    /// <summary>
    /// Gets or sets the raw request data
    /// </summary>
    /// <value>
    /// The raw.
    /// </value>
    public NameValueCollection Raw { get; }

    /// <summary>
    /// Gets the realm.
    /// </summary>
    public Realm Realm { get; }

    /// <summary>
    /// The options for the realm.
    /// </summary>
    public RealmOptions Options => Realm.Options;

    /// <summary>
    /// The options for the server.
    /// </summary>
    public ServerOptions ServerOptions => Options.ServerOptions;
}
