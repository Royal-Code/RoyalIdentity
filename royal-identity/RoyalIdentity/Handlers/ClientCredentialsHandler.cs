using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Events;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Handlers;

public class ClientCredentialsHandler : IHandler<ClientCredentialsContext>
{
    private readonly ILogger logger;
    private readonly ITokenFactory tokenFactory;
    private readonly IEventDispatcher eventDispatcher;

    public ClientCredentialsHandler(
        ILogger<ClientCredentialsHandler> logger,
        ITokenFactory tokenFactory,
        IEventDispatcher eventDispatcher)
    {
        this.logger = logger;
        this.tokenFactory = tokenFactory;
        this.eventDispatcher = eventDispatcher;
    }

    public async Task Handle(ClientCredentialsContext context, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClientSecret();
        context.AssertResourcesValidated();
        var client = context.ClientParameters.Client;

        logger.LogDebug("Handle client credentials context start");

        // create the access token for the client
        var request = new AccessTokenRequest()
        {
            HttpContext = context.HttpContext,
            User = context.GetSubject()!,
            Client = client,
            Resources = context.Resources,
            IdentityType = OidcConstants.IdentityProfileTypes.Client,
        };

        var accessToken = await tokenFactory.CreateAccessTokenAsync(request, ct);

        var atEvent = new AccessTokenIssuedEvent(context, new Token(OidcConstants.TokenTypes.AccessToken, accessToken.Token));

        logger.LogDebug("Access token issued");

        context.Response = new TokenResponse(
            accessToken,
            null,
            null,
            context.Resources.RequestedScopes.ToSpaceSeparatedString());

        await eventDispatcher.DispatchAsync(atEvent);

        logger.LogDebug("Handle client credentials context end");
    }
}
