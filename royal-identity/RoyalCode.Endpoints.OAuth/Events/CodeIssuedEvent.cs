using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;

namespace RoyalIdentity.Events
{
    public class CodeIssuedEvent : Event
    {
        public CodeIssuedEvent(AuthorizeContext context, Token code)
            : base(EventCategories.Authorize, "Code Issued Success", EventTypes.Success)
        {
            Context = context;
            Code = code;
        }

        public AuthorizeContext Context { get; }

        public Token Code { get; }
    }
}