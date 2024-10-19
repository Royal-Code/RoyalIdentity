using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts;

public interface ISessionStateGenerator
{
    public string GenerateSessionStateValue(AuthorizeContext context);
}
