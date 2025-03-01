using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class LoadClient : IDecorator<IWithClient>
{
    private readonly ServerOptions options;
    private readonly IStorage storage;
    private readonly ILogger logger;

    public LoadClient(IStorage storage, ILogger<LoadClient> logger)
    {
        this.storage = storage;
        this.logger = logger;

        options = storage.ServerOptions;
    }

    public async Task Decorate(IWithClient context, Func<Task> next, CancellationToken ct)
    {
        var clientId = context.ClientId;

        if (clientId.IsMissing() && !context.IsClientRequired)
        {
            await next();
            return;
        }

        if (clientId.IsMissingOrTooLong(options.InputLengthRestrictions.ClientId))
        {
            logger.LogError(context, "The parameter client_id is missing or too long");
            context.InvalidRequest("Invalid client_id");
            return;
        }

        var clients = storage.GetClientStore(context.Realm);
        var client = await clients.FindEnabledClientByIdAsync(clientId, ct);
        if (client is null)
        {
            logger.LogError(context, "Unknown client or not enabled", clientId);
            context.InvalidRequest(OidcConstants.AuthorizeErrors.UnauthorizedClient, "Unknown client or client not enabled");
            return;
        }

        context.ClientParameters.SetClient(client);

        await next();
    }
}
