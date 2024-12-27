using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class EvaluateClient : IDecorator<IWithClientCredentials>
{
    private readonly IClientSecretChecker clientSecretChecker;
    private readonly ILogger logger;

    public EvaluateClient(IClientSecretChecker clientSecretChecker, ILogger<EvaluateClient> logger)
    {
        this.clientSecretChecker = clientSecretChecker;
        this.logger = logger;
    }

    public async Task Decorate(IWithClientCredentials context, Func<Task> next, CancellationToken ct)
    {
        logger.LogDebug("Start client evaluation");

        var evaluatedClient = await clientSecretChecker.EvaluateClientAsync(context, ct);
        if (evaluatedClient is null)
        {
            logger.LogError("No client identifier found");

            context.InvalidClient("No client identified");
            return;
        }

        if (!evaluatedClient.Credential.IsValid)
        {
            logger.LogError("Client secret validation failed for client: {Name} ({Id}).",
                evaluatedClient.Client.Name,
                evaluatedClient.Client.Id);

            context.InvalidClient("Client secret validation failed");
            return;
        }

        if (evaluatedClient.Client.RequireClientSecret && 
            evaluatedClient.Credential.Type is ServerConstants.ParsedSecretTypes.NoSecret)
        {
            logger.LogError("Client secret not informed for client: {Name} ({Id})",
                evaluatedClient.Client.Name,
                evaluatedClient.Client.Id);

            context.InvalidClient("Client secret validation failed");
            return;
        }

        if (!evaluatedClient.Client.Enabled)
        {
            logger.LogError("Client not enabled: {Name} ({Id})",
                evaluatedClient.Client.Name,
                evaluatedClient.Client.Id);

            context.InvalidClient("Client not found");
            return;
        }

        context.SetClientAndSecret(evaluatedClient.Client, evaluatedClient.Credential);
        context.Items.GetOrCreate<Asserts>().HasClient = true;

        await next();
    }
}
