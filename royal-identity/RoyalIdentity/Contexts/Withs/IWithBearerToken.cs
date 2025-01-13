using RoyalIdentity.Contexts.Parameters;
using RoyalIdentity.Endpoints.Abstractions;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithBearerToken : IContextBase
{
    /// <summary>
    /// The access token from the request.
    /// </summary>
    public string Token { get; }

    public BearerParameters BearerParameters { get; }
}
