using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
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

    public async Task<ParsedSecret?> ParseAsync(IEndpointContextBase context, CancellationToken ct)
    {
        // see if a registered parser finds a secret on the request
        ParsedSecret? bestSecret = null;
        foreach (var parser in evaluators)
        {
            var parsedSecret = await parser.EvaluateAsync(context, ct);
            if (parsedSecret == null)
                continue;

            logger.LogDebug("Parser found secret: {Type}", parser.GetType().Name);

            bestSecret = parsedSecret;

            if (parsedSecret.Type is not ServerConstants.ParsedSecretTypes.NoSecret)
                break;
        }

        if (bestSecret is not null)
        {
            logger.LogDebug("Secret id found: {Id}", bestSecret.Id);
            return bestSecret;
        }

        logger.LogDebug("Parser found no secret");
        return null;
    }

    public IEnumerable<string> GetAvailableAuthenticationMethods()
    {
        return evaluators.Select(p => p.AuthenticationMethod).Where(p => !string.IsNullOrWhiteSpace(p));
    }
}