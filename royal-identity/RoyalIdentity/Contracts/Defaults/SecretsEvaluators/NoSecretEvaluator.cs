using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public class NoSecretEvaluator : SecretEvaluatorBase
{
    public NoSecretEvaluator(
        IStorage storage,
        TimeProvider clock,
        ILogger<NoSecretEvaluator> logger) : base(storage, clock, logger)
    { }

    public override string AuthenticationMethod => string.Empty;

    /// <summary>
    /// Only reached when the request presented no credential at all; the preflight guarantees it, so the check
    /// for an assertion, a secret or an Authorization header that used to live here would now be dead code.
    /// </summary>
    public override ClientAuthenticationSource Source => ClientAuthenticationSource.None;

    protected override EvaluatedCredential InvalidCredentials => throw new InvalidOperationException("No secret evaluator should not be used");

    public override async Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start parsing and evaluate No secret");

        var hasClientId = context.Raw.TryGet(Oidc.Token.Request.ClientId, out var clientId);
        if (!hasClientId)
        {
            logger.LogDebug("Client id not found in post body");
            return null;
        }

        if (clientId!.Length > context.Options.InputLengthRestrictions.ClientId)
        {
            logger.LogError("Client ID exceeds maximum length.");
            return null;
        }

        // load client
        var clientStore = storage.GetClientStore(context.Realm);
        var client = await clientStore.FindEnabledClientByIdAsync(clientId, ct);
        if (client is null)
        {
            logger.LogError(context, $"No client with id '{clientId}' found. aborting client evaluation");
            return null;
        }

        if (client.RequireClientSecret)
        {
            logger.LogError(context, $"Client '{clientId}' is configured to require a secret. aborting client evaluation");
            return null;
        }

        return new EvaluatedClient(client, new EvaluatedCredential(Server.ParsedSecretTypes.NoSecret, true), string.Empty);
    }

}
