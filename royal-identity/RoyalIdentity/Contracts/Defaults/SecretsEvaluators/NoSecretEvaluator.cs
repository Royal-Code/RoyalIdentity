using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public class NoSecretEvaluator : SecretEvaluatorBase
{
    public NoSecretEvaluator(
        IClientStore clientStore,
        IOptions<ServerOptions> options,
        TimeProvider clock,
        ILogger<NoSecretEvaluator> logger) : base(clientStore, options.Value, clock, logger)
    { }

    public override string AuthenticationMethod => string.Empty;

    protected override EvaluatedCredential InvalidCredentials => throw new InvalidOperationException("No secret evaluator should not be used");

    public override async Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start parsing and evaluate No secret");

        // when there is a secret, assertion, or authorization header, the client will not be evaluated
        if (context.Raw.TryGet(OidcConstants.TokenRequest.ClientAssertion, out _) ||
            context.Raw.TryGet(OidcConstants.TokenRequest.ClientSecret, out _) ||
            context.HttpContext.Request.Headers.Authorization.FirstOrDefault().IsPresent())
        {
            logger.LogDebug("Client assertion, or secret, or authorization header found in post body, aborting client evaluation");
            return null;
        }

        var hasClientId = context.Raw.TryGet(OidcConstants.TokenRequest.ClientId, out var clientId);
        if (!hasClientId)
        {
            logger.LogDebug("Client id not found in post body");
            return null;
        }

        if (clientId!.Length > options.InputLengthRestrictions.ClientId)
        {
            logger.LogError("Client ID exceeds maximum length.");
            return null;
        }

        // load client
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

        return new EvaluatedClient(client, new EvaluatedCredential(ServerConstants.ParsedSecretTypes.NoSecret, true), string.Empty);
    }

}