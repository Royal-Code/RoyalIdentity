using Microsoft.Extensions.Logging;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Contexts.Decorators;

public class ClientIdentityDecorator : IDecorator<ClientCredentialsContext>
{
    private readonly ILogger logger;
    private readonly IUserDetailsStore userDetailsStore;

    public Task Decorate(ClientCredentialsContext context, Func<Task> next, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClientSecret();
        var client = context.ClientParameters.Client;

        logger.LogDebug("Client Identity decorator start");

        // check if client has a identity username, or user client id as username to find the user details

        throw new NotImplementedException();
    }
}
