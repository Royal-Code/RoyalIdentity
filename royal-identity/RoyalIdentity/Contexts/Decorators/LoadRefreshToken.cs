using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Decorators;

public class LoadRefreshToken : IDecorator<RefreshTokenContext>
{
    private readonly IRefreshTokenStore store;
    private readonly TimeProvider clock;
    private readonly ILogger logger;

    public LoadRefreshToken(IRefreshTokenStore store, TimeProvider clock, ILogger<LoadRefreshToken> logger)
    {
        this.store = store;
        this.clock = clock;
        this.logger = logger;
    }

    public async Task Decorate(RefreshTokenContext context, Func<Task> next, CancellationToken ct)
    {
        logger.LogDebug("Start loading refresh token");

        context.AssertHasClient();

        var options = context.Items.GetOrCreate<ServerOptions>();
        var client = context.Client;
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

        if (token.Length > options.InputLengthRestrictions.RefreshToken)
        {
            logger.LogError(context, "Refresh token too long");
            context.InvalidRequest("Refresh token too long");
            return;
        }

        var refreshToken = await store.GetAsync(token, ct);
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

        /////////////////////////////////////////////
        // check if refresh token has been consumed
        /////////////////////////////////////////////
        if (refreshToken.ConsumedTime.HasValue && 
            client.RefreshTokenPostConsumedTimeTolerance != TimeSpan.MaxValue)
        {
            bool doNotAcceptConsumedToken = client.RefreshTokenPostConsumedTimeTolerance == TimeSpan.Zero
                || refreshToken.ConsumedTime.HasExceeded(client.RefreshTokenPostConsumedTimeTolerance, clock.GetUtcNow().DateTime);

            if (doNotAcceptConsumedToken)
            {
                logger.LogWarning("Rejecting refresh token because it has been consumed already.");
                context.InvalidGrant("Refresh token has been consumed already.");
                return;
            }
        }

        context.RefreshToken = refreshToken;
        context.TokenFirstConsumedAt = refreshToken.ConsumedTime;
        context.Items.GetOrCreate<Asserts>().HasToken = true;

        await next();
    }
}
