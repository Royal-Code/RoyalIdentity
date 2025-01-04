using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class ClientResourceHandler : IHandler<ClientCredentialsContext>
{
    public Task Handle(ClientCredentialsContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
