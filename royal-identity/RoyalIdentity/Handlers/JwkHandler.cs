using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class JwkHandler : IHandler<JwkContext>
{
    private readonly IKeyManager keys;
    private readonly ILogger logger;

    public JwkHandler(IKeyManager keys, ILogger logger)
    {
        this.keys = keys;
        this.logger = logger;
    }

    public Task Handle(JwkContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
