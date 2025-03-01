using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Handlers;

public class JwkHandler : IHandler<JwkContext>
{
    private readonly IKeyManager keys;
    private readonly ILogger logger;

    public JwkHandler(IKeyManager keys, ILogger<JwkHandler> logger)
    {
        this.keys = keys;
        this.logger = logger;
    }

    public async Task Handle(JwkContext context, CancellationToken ct)
    {
        logger.LogDebug("Handling JWK request.");

        var validationKeys = await keys.GetValidationKeysAsync(context.Realm, ct);

        var webKeys = validationKeys.Jwks;

        logger.LogDebug("Web Keys loaded ({Count}).", webKeys.Count);

        context.Response = new JwkResponse(webKeys, context.Realm.Options.Discovery.ResponseCacheInterval);
    }
}
