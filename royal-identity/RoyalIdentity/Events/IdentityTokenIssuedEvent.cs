using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;

namespace RoyalIdentity.Events;

public class IdentityTokenIssuedEvent : Event
{
    public IdentityTokenIssuedEvent(IEndpointContextBase context, Token idt)
        : base(EventCategories.Authorize, "Identity Token Issued Success", EventTypes.Success)
    {
        Context = context;
        IdentityToken = idt;
    }

    public IEndpointContextBase Context { get; }

    public Token IdentityToken { get; }
}