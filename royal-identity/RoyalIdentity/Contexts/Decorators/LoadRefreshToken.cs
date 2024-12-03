using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using static RoyalIdentity.Options.OidcConstants;

namespace RoyalIdentity.Contexts.Decorators;

public class LoadRefreshToken : IDecorator<RefreshTokenContext>
{
    private readonly IRefreshTokenStore store;
    private readonly ILogger logger;


    public async Task Decorate(RefreshTokenContext context, Func<Task> next, CancellationToken ct)
    {
        logger.LogDebug("Start loading refresh token");

        ServerOptions options = context.Items.GetOrCreate<ServerOptions>();

        var token = context.Token;
        if (token.IsMissing())
        {
            logger.LogError(context, "Refresh token is missing");
            context.Error(TokenErrors.InvalidRequest, "Refresh token is missing");
            return;
        }

        if (token.Length > options.InputLengthRestrictions.RefreshToken)
        {
            logger.LogError(context, "Refresh token too long");
            context.Error(TokenErrors.InvalidGrant, "Refresh token too long");
            return;
        }

        var refreshToken = await store.GetAsync(token, ct);
        if (refreshToken is null)
        {
            logger.LogWarning("Invalid refresh token");
            context.Error(TokenErrors.InvalidGrant, "Invalid refresh token");
        }

        throw new NotImplementedException();
    }
}
