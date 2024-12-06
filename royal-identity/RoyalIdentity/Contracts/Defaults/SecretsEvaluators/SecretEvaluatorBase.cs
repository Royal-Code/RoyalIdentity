using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contracts.Defaults.SecretsEvaluators;

public abstract class SecretEvaluatorBase : IClientSecretEvaluator
{
    protected readonly IClientStore clientStore;
    protected readonly ServerOptions options;
    protected readonly TimeProvider clock;
    protected readonly ILogger logger;

    protected SecretEvaluatorBase(
        IClientStore clientStore,
        ServerOptions options,
        TimeProvider clock,
        ILogger logger)
    {
        this.clientStore = clientStore;
        this.options = options;
        this.clock = clock;
        this.logger = logger;
    }

    protected abstract EvaluatedCredential InvalidCredentials { get; }

    public abstract string AuthenticationMethod { get; }

    public abstract Task<EvaluatedClient?> EvaluateAsync(IEndpointContextBase context, CancellationToken ct);

    protected async Task<EvaluatedClient?> EvaluateAsync(
        IEndpointContextBase context,
        string clientId,
        string secret,
        string secretType,
        CancellationToken ct)
    {
        if (clientId.Length > options.InputLengthRestrictions.ClientId)
        {
            logger.LogError(context, "Client ID exceeds maximum length.");
            return null;
        }

        if (secret.Length > options.InputLengthRestrictions.ClientSecret)
        {
            logger.LogError(context, "Client secret exceeds maximum length.");
            return null;
        }

        // load client
        var client = await clientStore.FindEnabledClientByIdAsync(clientId, ct);
        if (client is null)
        {
            logger.LogError(context, $"No client with id '{clientId}' found. aborting client evaluation");

            return null;
        }

        // get secrets
        var sharedSecrets = client.ClientSecrets
            .Where(s => s.Type == ServerConstants.SecretTypes.SharedSecret)
            .Where(s => !s.Expiration.HasExpired(clock.GetUtcNow().UtcDateTime))
            .ToList();

        // validate secret.
        foreach (var sharedSecret in sharedSecrets)
        {
            var secretDescription = string.IsNullOrEmpty(sharedSecret.Description) ? "no description" : sharedSecret.Description;

            bool isValid = false;
            byte[] secretBytes;

            try
            {
                secretBytes = Convert.FromBase64String(sharedSecret.Value);
            }
            catch (FormatException ex)
            {
                logger.LogError(context, ex, $"Secret: {secretDescription} uses invalid hashing algorithm.");
                return new EvaluatedClient(client, InvalidCredentials);
            }
            catch (ArgumentNullException ex)
            {
                logger.LogError(context, ex, $"Secret: {secretDescription} is null.");
                return new EvaluatedClient(client, InvalidCredentials);
            }

            switch (secretBytes.Length)
            {
                case 32:
                    isValid = TimeConstantComparer.IsEqual(sharedSecret.Value, secret.Sha256());
                    break;
                case 64:
                    isValid = TimeConstantComparer.IsEqual(sharedSecret.Value, secret.Sha512());
                    break;
                default:
                    logger.LogError(context, $"Secret: {secretDescription} uses invalid hashing algorithm.");
                    return new EvaluatedClient(client, InvalidCredentials);
            }

            if (isValid)
            {
                return new EvaluatedClient(client, new EvaluatedCredential(secretType, true, sharedSecret));
            }
        }

        return new EvaluatedClient(client, InvalidCredentials);
    }


}