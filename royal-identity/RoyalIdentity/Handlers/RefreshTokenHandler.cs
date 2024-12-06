using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class RefreshTokenHandler : IHandler<RefreshTokenContext>
{
    public Task Handle(RefreshTokenContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
