using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;

namespace RoyalIdentity.Contexts;

public interface ITokenEndpointContextBase : IWithClientCredentials
{
    public string GrantType { get; }

    void Load(ILogger logger);
}
