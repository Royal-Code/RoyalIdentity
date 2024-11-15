using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public class PostBodySecretEvaluator : SecretEvaluatorBase
{
    private static readonly EvaluatedCredential PostBodyInvalidCredentials =
        new (ServerConstants.ParsedSecretTypes.SharedSecret, false);

    public PostBodySecretEvaluator(IClientStore clientStore,
        IOptions<ServerOptions> options,
        TimeProvider clock,
        ILogger<PostBodySecretEvaluator> logger) : base(clientStore, options.Value, clock, logger)
    { }

    protected override EvaluatedCredential InvalidCredentials => PostBodyInvalidCredentials;

    public override string AuthenticationMethod => OidcConstants.EndpointAuthenticationMethods.PostBody;

    public override async Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start parsing and evaluate PostBody Authentication secret");

        var hasClientId = context.Raw.TryGet(OidcConstants.TokenRequest.ClientId, out var clientId);
        var hasSecret = context.Raw.TryGet(OidcConstants.TokenRequest.ClientSecret, out var secret);

        if (!hasClientId || !hasSecret)
        {
            logger.LogDebug("Client id or secret not found in post body");
            return null;
        }

        return await EvaluateAsync(context, clientId, secret, ServerConstants.ParsedSecretTypes.SharedSecret, ct);
    }
}