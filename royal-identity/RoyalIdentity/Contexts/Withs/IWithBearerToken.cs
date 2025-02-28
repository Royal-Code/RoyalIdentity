using RoyalIdentity.Contexts.Parameters;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithBearerToken : IEndpointContextBase
{
    /// <summary>
    /// The access token from the request.
    /// </summary>
    public string Token { get; }

    public BearerParameters BearerParameters { get; }
}
