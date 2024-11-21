using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class LoadClient : IDecorator<IWithClient>
{
    private readonly ServerOptions options;
    private readonly IClientStore clients;
    private readonly ILogger logger;

    public LoadClient(IOptions<ServerOptions> options, IClientStore clients, ILogger<LoadClient> logger)
    {
        this.options = options.Value;
        this.clients = clients;
        this.logger = logger;
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
            logger.LogError(options, "The parameter client_id is missing or too long", context);
            context.InvalidRequest("Invalid client_id");
            return;
        }

        var client = await clients.FindEnabledClientByIdAsync(clientId, ct);
        if (client is null)
        {
            logger.LogError(options, "Unknown client or not enabled", clientId, context);
            context.InvalidRequest(OidcConstants.AuthorizeErrors.UnauthorizedClient, "Unknown client or client not enabled");
            return;
        }

        context.SetClient(client);
        context.Items.GetOrCreate<Asserts>().HasClient = true;

        await next();
    }
}
