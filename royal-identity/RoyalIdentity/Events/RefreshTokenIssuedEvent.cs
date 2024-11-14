using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts;

namespace RoyalIdentity.Events;

public class RefreshTokenIssuedEvent : Event
{
    public RefreshTokenIssuedEvent(IEndpointContextBase context, Token rt)
        : base(EventCategories.Authorize, "Refresh Token Issued Success", EventTypes.Success)
    {
        Context = context;
        RefreshToken = rt;
    }

    public IEndpointContextBase Context { get; }

    public Token RefreshToken { get; }
}
