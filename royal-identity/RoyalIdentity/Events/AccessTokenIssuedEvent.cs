using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;

namespace RoyalIdentity.Events;

public class AccessTokenIssuedEvent : Event
{
    public AccessTokenIssuedEvent(IEndpointContextBase context, Token code)
        : base(EventCategories.Authorize, "Access Token Issued Success", EventTypes.Success)
    {
        Context = context;
        Code = code;
    }

    public IEndpointContextBase Context { get; }

    public Token Code { get; }
}
