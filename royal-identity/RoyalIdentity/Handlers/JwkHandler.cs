using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Handlers;

public class JwkHandler : IHandler<JwkContext>
{
    private readonly IKeyManager keys;

    public JwkHandler(IKeyManager keys)
    {
        this.keys = keys;
    }

    public async Task Handle(JwkContext context, CancellationToken ct)
    {
        var validationKeys = await keys.GetValidationKeysAsync(context.Realm, ct);

        var webKeys = validationKeys.Jwks;

        context.Response = new JwkResponse(webKeys, context.Realm.Options.Discovery.ResponseCacheInterval);
    }
}
