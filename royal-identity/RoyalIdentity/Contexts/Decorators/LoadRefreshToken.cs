using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Decorators;

public class LoadRefreshToken : IDecorator<RefreshTokenContext>
{
    private readonly IStorage storage;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public LoadRefreshToken(IStorage storage, TimeProvider clock, ILogger<LoadRefreshToken> logger)
    {
        this.storage = storage;
        this.clock = clock;
        this.logger = logger;
    }

    public async Task Decorate(RefreshTokenContext context, Func<Task> next, CancellationToken ct)
    {
        logger.LogDebug("Start loading refresh token");

        context.ClientParameters.AssertHasClient();

        var restrictions = context.Options.InputLengthRestrictions;
        var client = context.ClientParameters.Client;
        var token = context.Token;

        /////////////////////////////////////////////
        // check if refresh token is valid
        /////////////////////////////////////////////
        if (token.IsMissing())
        {
            logger.LogError(context, "Refresh token is missing");
            context.InvalidRequest("Refresh token is missing");
            return;
        }

        if (token.Length > restrictions.RefreshToken)
        {
            logger.LogError(context, "Refresh token too long");
            context.InvalidRequest("Refresh token too long");
            return;
        }

        var refreshToken = await storage.GetRefreshTokenStore(context.Realm).GetAsync(token, ct);
        if (refreshToken is null)
        {
            logger.LogWarning("Invalid refresh token");
            context.InvalidGrant("Invalid refresh token");
            return;
        }

        /////////////////////////////////////////////
        // check if refresh token has expired
        /////////////////////////////////////////////
        if (refreshToken.CreationTime.HasExceeded(refreshToken.Lifetime, clock.GetUtcNow().DateTime))
        {
            logger.LogWarning("Refresh token has expired.");
            context.InvalidGrant("Refresh token has expired");
            return;
        }

        /////////////////////////////////////////////
        // check if client belongs to requested refresh token
        /////////////////////////////////////////////
        if (client.Id != refreshToken.ClientId)
        {
            logger.LogError("{ClientId} tries to refresh token belonging to {RefreshTokenClientId}", client.Id, refreshToken.ClientId);
            context.InvalidGrant("Invalid client");
            return;
        }

        /////////////////////////////////////////////
        // check if client still has offline_access scope
        /////////////////////////////////////////////
        if (!client.AllowOfflineAccess)
        {
            logger.LogError("{ClientId} does not have access to offline_access scope anymore", client.Id);
            context.InvalidGrant("Invalid client");
            return;
        }

        // Consumption and the post-consumption tolerance are deliberately NOT checked here. The state read now
        // can be stale by the time the token is actually consumed, so deciding on it would either reject a token
        // another request had not consumed yet, or accept one it had. Both belong to the handler, next to the
        // conditional transition, over the state that transition rematerializes
        // (plan-data-operational-storage DF12/DF37).

        context.RefreshParameters.SetRefreshToken(refreshToken);

        await next();
    }
}
