using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;

namespace RoyalIdentity.Events;

public class IdentityTokenIssuedEvent : Event
{
    public IdentityTokenIssuedEvent(IEndpointContextBase context, Token code)
        : base(EventCategories.Authorize, "Identity Token Issued Success", EventTypes.Success)
    {
        Context = context;
        Code = code;
    }

    public IEndpointContextBase Context { get; }

    public Token Code { get; }
}