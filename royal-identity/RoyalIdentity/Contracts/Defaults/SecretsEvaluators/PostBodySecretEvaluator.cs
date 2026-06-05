using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public class PostBodySecretEvaluator : SecretEvaluatorBase
{
    private static readonly EvaluatedCredential PostBodyInvalidCredentials =
        new (Server.ParsedSecretTypes.SharedSecret, false);

    public PostBodySecretEvaluator(
        IStorage storage,
        TimeProvider clock,
        ILogger<PostBodySecretEvaluator> logger) : base(storage, clock, logger)
    { }

    protected override EvaluatedCredential InvalidCredentials => PostBodyInvalidCredentials;

    public override string AuthenticationMethod => Oidc.Endpoint.AuthMethods.PostBody;

    public override async Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start parsing and evaluate PostBody Authentication secret");

        var hasClientId = context.Raw.TryGet(Oidc.Token.Request.ClientId, out var clientId);
        var hasSecret = context.Raw.TryGet(Oidc.Token.Request.ClientSecret, out var secret);

        if (!hasClientId || !hasSecret)
        {
            logger.LogDebug("Client id or secret not found in post body");
            return null;
        }

        return await EvaluateAsync(context, clientId!, secret!, Server.ParsedSecretTypes.SharedSecret, ct);
    }
}