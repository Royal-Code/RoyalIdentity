using System.Security.Claims;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;

namespace RoyalIdentity.Contexts;

public interface ITokenEndpointContextBase : IWithClientCredentials
{
    public string GrantType { get; }

    public ClaimsPrincipal? GetSubject();

    void Load(ILogger logger);
}
