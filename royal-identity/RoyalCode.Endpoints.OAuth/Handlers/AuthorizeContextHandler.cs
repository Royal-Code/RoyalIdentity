using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class AuthorizeContextHandler : IHandler<AuthorizeContext>
{
    public ValueTask Handle(AuthorizeContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
