using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Handlers;

public class JwkHandler : IHandler<JwkContext>
{
    private readonly IKeyManager keys;
    private readonly ServerOptions options;

    public JwkHandler(IKeyManager keys, IOptions<ServerOptions> options)
    {
        this.keys = keys;
        this.options = options.Value;
    }

    public async Task Handle(JwkContext context, CancellationToken ct)
    {
        var validationKeys = await keys.GetValidationKeysAsync(ct);

        var webKeys = validationKeys.Jwks;

        context.Response = new JwkResponse(webKeys, options.Discovery.ResponseCacheInterval);
    }
}
