using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithAcr : IEndpointContextBase
{
    /// <summary>
    /// Gets or sets the authentication context reference classes.
    /// </summary>
    /// <value>
    /// The authentication context reference classes.
    /// </value>
    public HashSet<string> AcrValues { get; }
}
