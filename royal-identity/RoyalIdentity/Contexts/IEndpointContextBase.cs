using RoyalIdentity.Endpoints.Abstractions;
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
}
