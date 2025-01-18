using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultClientSecretChecker : IClientSecretChecker
{
    private readonly IEnumerable<IClientSecretEvaluator> evaluators;
    private readonly ILogger<DefaultClientSecretChecker> logger;

    public DefaultClientSecretChecker(
        IEnumerable<IClientSecretEvaluator> evaluators,
        ILogger<DefaultClientSecretChecker> logger)
    {
        this.evaluators = evaluators;
        this.logger = logger;
    }

    public async Task<EvaluatedClient?> EvaluateClientAsync(IEndpointContextBase context, CancellationToken ct)
    {
        logger.LogDebug("Start evaluate client secret");

        // see if a registered evaluators finds a secret on the request
        EvaluatedClient? evaluation = null;
        foreach (var evaluator in evaluators)
        {
            var evaluatedClient = await evaluator.EvaluateAsync(context, ct);
            if (evaluatedClient is null)
                continue;

            logger.LogDebug("Evaluator found secret: {Type}", evaluator.GetType().Name);

            evaluation = evaluatedClient;

            if (evaluatedClient.Credential.Type is not ServerConstants.ParsedSecretTypes.NoSecret)
                break;
        }

        if (evaluation is not null)
        {
            var isValid = evaluation.Credential.IsValid ? "secret is valid" : "secret is not valid";

            logger.LogDebug("Client evaluated: {Type}, {Name} ({Id}), {Method}, {IsValid}", 
                evaluation.Credential.Type, 
                evaluation.Client.Name,
                evaluation.Client.Id,
                evaluation.AuthenticationMethod,
                isValid);

            return evaluation;
        }

        logger.LogDebug("Client not found");

        return null;
    }

    public IEnumerable<string> GetAvailableAuthenticationMethods()
    {
        return evaluators.Select(p => p.AuthenticationMethod).Where(p => p.IsPresent());
    }
}