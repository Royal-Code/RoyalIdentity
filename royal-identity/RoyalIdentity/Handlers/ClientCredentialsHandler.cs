using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Handlers;

public class ClientCredentialsHandler : IHandler<ClientCredentialsContext>
{
    private readonly ILogger logger;
    private readonly ITokenFactory tokenFactory;

    public async Task Handle(ClientCredentialsContext context, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClientSecret();
        context.AssertResourcesValidated();
        var client = context.ClientParameters.Client;

        logger.LogDebug("Handle client credentials context start");

        AccessToken newAccessToken;

        // create a session for the client authorization

        var request = new AccessTokenRequest()
        {
            HttpContext = context.HttpContext,
            User = context.GetSubject()!,
            Client = client,
            Resources = context.Resources,
            Caller = nameof(ClientCredentialsHandler),
        };


        newAccessToken = await tokenFactory.CreateAccessTokenAsync(request, ct);

        throw new NotImplementedException();
    }
}
