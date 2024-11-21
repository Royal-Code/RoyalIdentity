using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class EndSessionHandler : IHandler<EndSessionContext>
{
    public Task Handle(EndSessionContext context, CancellationToken ct)
    {


        throw new NotImplementedException();
    }
}
