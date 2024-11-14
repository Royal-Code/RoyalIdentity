using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultClientSecretChecker : IClientSecretChecker
{
    private readonly IEnumerable<IClientSecretsEvaluator> evaluators;
    private readonly ILogger<IClientSecretChecker> logger;

    public DefaultClientSecretChecker(
        IEnumerable<IClientSecretsEvaluator> evaluators,
        ILogger<DefaultClientSecretChecker> logger)
    {
        this.evaluators = evaluators;
        this.logger = logger;
    }

    public async Task<EvaluatedClient?> EvaluateClientAsync(IEndpointContextBase context, CancellationToken ct)
    {
        // see if a registered evaluators finds a secret on the request
        EvaluatedClient? evaluation = null;
        foreach (var evaluator in evaluators)
        {
            var evaluatedClient = await evaluator.EvaluateAsync(context, ct);
            if (evaluatedClient is null)
                continue;

            logger.LogDebug("Parser found secret: {Type}", evaluator.GetType().Name);

            evaluation = evaluatedClient;

            if (evaluatedClient.Credential.Type is not ServerConstants.ParsedSecretTypes.NoSecret)
                break;
        }

        if (evaluation is not null)
        {
            logger.LogDebug("Client evaluated: {Type}, {Name} ({Id})", 
                evaluation.Credential.Type, 
                evaluation.Client.Name,
                evaluation.Client.Id);

            return evaluation;
        }

        logger.LogDebug("Client not found");

        return null;
    }

    public IEnumerable<string> GetAvailableAuthenticationMethods()
    {
        return evaluators.Select(p => p.AuthenticationMethod).Where(p => !string.IsNullOrWhiteSpace(p));
    }
}