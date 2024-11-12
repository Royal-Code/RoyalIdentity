using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class AuthorizationCodeHandler : IHandler<AuthorizationCodeContext>
{
    public Task Handle(AuthorizationCodeContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
