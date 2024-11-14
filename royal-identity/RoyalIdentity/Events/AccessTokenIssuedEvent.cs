using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;

namespace RoyalIdentity.Events;

public class AccessTokenIssuedEvent : Event
{
    public AccessTokenIssuedEvent(IEndpointContextBase context, Token at)
        : base(EventCategories.Authorize, "Access Token Issued Success", EventTypes.Success)
    {
        Context = context;
        AccessToken = at;
    }

    public IEndpointContextBase Context { get; }

    public Token AccessToken { get; }
}
